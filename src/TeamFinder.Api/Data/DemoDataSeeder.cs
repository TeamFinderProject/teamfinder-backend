using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Data;

/// <summary>
/// Демо-данные для показа на checkpoint (docs/demo-guide.md). Включаются настройкой Demo:SeedOnStartup.
/// Создаются один раз: если демо-владелец уже есть, ничего не делаем.
///
/// Что получается:
/// - ivan@demo.local — владелец проекта «TeamFinder» (идёт набор): Backend 2 места, Frontend 1 место (занято), Designer 1 место;
/// - petr@demo.local — кандидат с навыками C#, PostgreSQL, Docker; ждёт решения по заявке на Backend;
/// - anna@demo.local — уже в команде «TeamFinder» на роли Frontend;
/// - maria@demo.local — владелец хакатона (идёт набор) и проекта «Бот для расписания» (в работе);
/// у всех пароль Demo12345!
/// </summary>
public static class DemoDataSeeder
{
    public const string Password = "Demo12345!";

    public static async Task SeedAsync(TeamFinderDbContext db, IPasswordHasher<User> hasher, TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == "ivan@demo.local", ct))
            return;

        var now = clock.GetUtcNow();
        User Person(string email, string name, string bio)
        {
            var user = new User { Email = email, CreatedAt = now.AddDays(-7) };
            user.PasswordHash = hasher.HashPassword(user, Password);
            user.Profile = new Profile { DisplayName = name, Bio = bio, UpdatedAt = now };
            db.Users.Add(user);
            return user;
        }

        var ivan = Person("ivan@demo.local", "Иван", "Студент, придумал TeamFinder и собирает команду");
        var petr = Person("petr@demo.local", "Пётр", "Пишу backend на C#, хочу в командный проект");
        var anna = Person("anna@demo.local", "Анна", "Frontend на React");
        var maria = Person("maria@demo.local", "Мария", "Организую хакатоны");
        await db.SaveChangesAsync(ct);

        // Навыки и интересы (id из SeedData): 1 C#, 3 PostgreSQL, 6 TypeScript, 7 React, 9 Docker, 13 Figma, 18 UI/UX.
        void Skill(User u, long skillId, SkillLevel level) =>
            db.UserSkills.Add(new UserSkill { UserId = u.Id, SkillId = skillId, Level = level, CreatedAt = now });
        void Interest(User u, long roleTypeId) =>
            db.UserRoleInterests.Add(new UserRoleInterest { UserId = u.Id, RoleTypeId = roleTypeId, CreatedAt = now });

        Skill(petr, 1, SkillLevel.Advanced); Skill(petr, 3, SkillLevel.Intermediate); Skill(petr, 9, SkillLevel.Beginner);
        Interest(petr, 1);
        Skill(anna, 7, SkillLevel.Intermediate); Skill(anna, 6, SkillLevel.Beginner);
        Interest(anna, 2);
        Skill(maria, 13, SkillLevel.Advanced); Skill(maria, 18, SkillLevel.Intermediate);
        Interest(maria, 4);

        ProjectRole Role(long roleTypeId, string title, string description, int capacity, params (long SkillId, RequirementType Type, SkillLevel? Min)[] requirements)
        {
            var role = new ProjectRole
            {
                RoleTypeId = roleTypeId, Title = title, Description = description, Capacity = capacity, CreatedAt = now,
            };
            foreach (var r in requirements)
                role.SkillRequirements.Add(new RoleSkillRequirement { SkillId = r.SkillId, RequirementType = r.Type, MinimumLevel = r.Min });
            return role;
        }

        var teamFinder = new Project
        {
            OwnerId = ivan.Id,
            Title = "TeamFinder",
            Description = "Сервис поиска участников и формирования команд для pet-проектов, хакатонов и учебных проектов.",
            ProjectType = ProjectType.StudyProject,
            Status = ProjectStatus.Recruiting,
            ExternalContactUrl = "https://t.me/teamfinder_demo",
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-1),
        };
        var backend = Role(1, "Backend Developer", "Реализация API и бизнес-логики", 2,
            (1, RequirementType.Required, SkillLevel.Intermediate),
            (3, RequirementType.Required, SkillLevel.Beginner),
            (9, RequirementType.NiceToHave, null));
        var frontend = Role(2, "Frontend Developer", "Экраны на React", 1,
            (7, RequirementType.Required, SkillLevel.Beginner),
            (6, RequirementType.Required, null),
            (13, RequirementType.NiceToHave, null));
        var designer = Role(4, "Designer", "Макеты в Figma", 1, (13, RequirementType.Required, null));
        teamFinder.Roles.Add(backend);
        teamFinder.Roles.Add(frontend);
        teamFinder.Roles.Add(designer);

        var hackathon = new Project
        {
            OwnerId = maria.Id,
            Title = "Хакатон: трекер привычек",
            Description = "Мобильное приложение за 48 часов. Ищем дизайнера и двух backend-разработчиков.",
            ProjectType = ProjectType.Hackathon,
            Status = ProjectStatus.Recruiting,
            ExternalContactUrl = "https://t.me/habit_hackathon",
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now.AddDays(-3),
        };
        hackathon.Roles.Add(Role(4, "Designer", "Интерфейс приложения", 1, (13, RequirementType.Required, SkillLevel.Intermediate)));
        hackathon.Roles.Add(Role(1, "Backend Developer", "API на C#", 2, (1, RequirementType.Required, null), (9, RequirementType.NiceToHave, null)));

        var bot = new Project
        {
            OwnerId = maria.Id,
            Title = "Бот для расписания",
            Description = "Telegram-бот с расписанием занятий группы.",
            ProjectType = ProjectType.PetProject,
            Status = ProjectStatus.InProgress,
            CreatedAt = now.AddDays(-20),
            UpdatedAt = now.AddDays(-10),
        };
        var botBackend = Role(1, "Backend Developer", "Логика бота", 1, (1, RequirementType.Required, null));
        bot.Roles.Add(botBackend);

        db.Projects.AddRange(teamFinder, hackathon, bot);
        await db.SaveChangesAsync(ct);

        // Анна уже в команде TeamFinder на Frontend — роль заполнена, на ней видно «Мест нет».
        db.Applications.Add(new Application
        {
            UserId = anna.Id, ProjectId = teamFinder.Id, ProjectRoleId = frontend.Id, Status = ApplicationStatus.Accepted,
            Message = "Делала похожий проект на React", CreatedAt = now.AddDays(-4), DecidedAt = now.AddDays(-3),
        });
        db.Memberships.Add(new Membership { UserId = anna.Id, ProjectId = teamFinder.Id, ProjectRoleId = frontend.Id, JoinedAt = now.AddDays(-3) });

        // Пётр ждёт решения по Backend — владелец примет его на показе.
        db.Applications.Add(new Application
        {
            UserId = petr.Id, ProjectId = teamFinder.Id, ProjectRoleId = backend.Id, Status = ApplicationStatus.Pending,
            Message = "Есть опыт с C# и PostgreSQL, хочу заниматься backend", CreatedAt = now.AddHours(-5),
        });

        // Пётр — участник «Бота для расписания» (проект в работе).
        db.Applications.Add(new Application
        {
            UserId = petr.Id, ProjectId = bot.Id, ProjectRoleId = botBackend.Id, Status = ApplicationStatus.Accepted,
            CreatedAt = now.AddDays(-15), DecidedAt = now.AddDays(-14),
        });
        db.Memberships.Add(new Membership { UserId = petr.Id, ProjectId = bot.Id, ProjectRoleId = botBackend.Id, JoinedAt = now.AddDays(-14) });

        db.Notifications.Add(new Notification
        {
            UserId = ivan.Id, Type = NotificationType.ApplicationReceived, ProjectId = teamFinder.Id, CreatedAt = now.AddHours(-5),
            Text = "Новая заявка: Пётр на роль Backend Developer в проекте «TeamFinder»",
        });
        db.Notifications.Add(new Notification
        {
            UserId = anna.Id, Type = NotificationType.ApplicationAccepted, ProjectId = teamFinder.Id, CreatedAt = now.AddDays(-3),
            ReadAt = now.AddDays(-3), Text = "Вас приняли в проект «TeamFinder» на роль Frontend Developer",
        });

        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Common;
using TeamFinder.Api.Data;
using TeamFinder.Api.Entities;
using TeamFinder.Api.Notifications;
using TeamFinder.Api.Profiles;
using TeamFinder.Api.Projects;

namespace TeamFinder.Api.Applications;

/// <summary>
/// Подача и обработка заявок. Каждая изменяющая операция начинается с блокировки строки проекта
/// (ProjectLock), поэтому операции одного проекта выполняются строго по очереди.
/// </summary>
public sealed class ApplicationService(TeamFinderDbContext db, NotificationService notifications, TimeProvider clock)
{
    /// <summary>Заявка на конкретную роль конкретного проекта (RULE-APP-01).</summary>
    public async Task<ApplicationDto> ApplyAsync(long userId, long projectId, long roleId, ApplyRequest request, CancellationToken ct)
    {
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);

        var role = await db.ProjectRoles
            .Include(r => r.Project)
            .SingleOrDefaultAsync(r => r.Id == roleId && r.ProjectId == projectId, ct);
        if (role is null || (role.Project.Status == ProjectStatus.Draft && role.Project.OwnerId != userId))
            throw AppException.NotFound("Роль не найдена");
        var project = role.Project;

        if (project.OwnerId == userId)
            throw AppException.Conflict("OWNER_CANNOT_APPLY", "Нельзя подать заявку в собственный проект");
        if (project.Status != ProjectStatus.Recruiting)
            throw AppException.Conflict("PROJECT_NOT_RECRUITING", "Набор в этот проект сейчас не идёт");
        if (role.ClosedAt is not null)
            throw AppException.Conflict("ROLE_CLOSED", "Набор на эту роль закрыт");
        if (await db.Memberships.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, ct))
            throw AppException.Conflict("ALREADY_MEMBER", "Вы уже участвовали в команде этого проекта");
        if (await db.Applications.AnyAsync(a => a.ProjectRoleId == roleId && a.UserId == userId, ct))
            throw AppException.Conflict("DUPLICATE_APPLICATION", "Вы уже подавали заявку на эту роль");
        if (await OccupiedAsync(roleId, ct) >= role.Capacity)
            throw AppException.Conflict("ROLE_FULL", "Все места на этой роли уже заняты");

        var now = clock.GetUtcNow();
        var application = new Application
        {
            UserId = userId,
            ProjectId = projectId,
            ProjectRoleId = roleId,
            Status = ApplicationStatus.Pending,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            CreatedAt = now,
        };
        db.Applications.Add(application);

        var candidateName = await DisplayNameAsync(userId, ct);
        notifications.Add(project.OwnerId, NotificationType.ApplicationReceived,
            $"Новая заявка: {candidateName} на роль {role.Title} в проекте «{project.Title}»", projectId);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(application, null);
    }

    public async Task<IReadOnlyList<MyApplicationDto>> GetMineAsync(long userId, CancellationToken ct) =>
        await db.Applications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new MyApplicationDto(
                a.Id,
                new ProjectRefDto(a.ProjectId, a.Project.Title, a.Project.Status),
                new RoleRefDto(a.ProjectRoleId, a.ProjectRole.Title),
                a.Status, a.ClosureReason, a.Message, a.CreatedAt, a.DecidedAt,
                a.Status == ApplicationStatus.Pending))
            .ToListAsync(ct);

    /// <summary>Заявки проекта с данными кандидата и итогом matching — для владельца (RULE-PERM-02).</summary>
    public async Task<IReadOnlyList<ProjectApplicationDto>> GetForProjectAsync(
        long projectId, long userId, ApplicationStatus? status, long? roleId, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw AppException.NotFound("Проект не найден");
        if (project.OwnerId != userId)
            throw AppException.Forbidden("Заявки проекта видит только его владелец");

        var applications = await db.Applications.AsNoTracking()
            .Where(a => a.ProjectId == projectId
                        && (status == null || a.Status == status)
                        && (roleId == null || a.ProjectRoleId == roleId))
            .Include(a => a.User).ThenInclude(u => u.Profile)
            .Include(a => a.User).ThenInclude(u => u.Skills).ThenInclude(s => s.Skill)
            .Include(a => a.User).ThenInclude(u => u.RoleInterests).ThenInclude(i => i.RoleType)
            .Include(a => a.ProjectRole).ThenInclude(r => r.SkillRequirements).ThenInclude(s => s.Skill)
            .AsSplitQuery()
            .OrderBy(a => a.ProjectRoleId).ThenBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return applications.Select(a =>
        {
            var userSkills = a.User.Skills.ToDictionary(s => s.SkillId, s => s.Level);
            var interested = a.User.RoleInterests.Any(i => i.RoleTypeId == a.ProjectRole.RoleTypeId);
            var match = MatchingService.Compute(a.ProjectRole, userSkills, interested).Summary;
            var profile = ProfileService.ToDto(a.User);
            return new ProjectApplicationDto(
                a.Id, a.ProjectRoleId, a.ProjectRole.Title, a.Status, a.ClosureReason, a.Message, a.CreatedAt, a.DecidedAt,
                new CandidateDto(a.UserId, profile.DisplayName, profile.Bio, profile.GithubUrl, profile.Skills, profile.InterestedRoleTypes),
                match);
        }).ToList();
    }

    /// <summary>Отозвать можно только свою заявку и только в PENDING (RULE-APP-03).</summary>
    public async Task<ApplicationDto> WithdrawAsync(long applicationId, long userId, CancellationToken ct)
    {
        var projectId = await ProjectIdOfAsync(applicationId, ct);
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var application = await db.Applications.SingleAsync(a => a.Id == applicationId, ct);

        if (application.UserId != userId)
            throw AppException.Forbidden("Отозвать можно только свою заявку");
        EnsurePending(application);

        application.Status = ApplicationStatus.Withdrawn;
        application.DecidedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(application, null);
    }

    public async Task<ApplicationDto> RejectAsync(long applicationId, long userId, CancellationToken ct)
    {
        var projectId = await ProjectIdOfAsync(applicationId, ct);
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var application = await db.Applications
            .Include(a => a.Project).Include(a => a.ProjectRole)
            .SingleAsync(a => a.Id == applicationId, ct);

        EnsureOwner(application.Project, userId);
        EnsurePending(application);

        application.Status = ApplicationStatus.Rejected;
        application.DecidedAt = clock.GetUtcNow();
        notifications.Add(application.UserId, NotificationType.ApplicationRejected,
            $"Заявка на роль {application.ProjectRole.Title} в проекте «{application.Project.Title}» отклонена", projectId);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(application, null);
    }

    /// <summary>
    /// Принятие — одна транзакция под блокировкой проекта (RULE-APP-04, RULE-CAP-03):
    /// проверки → ACCEPTED → Membership → закрытие лишних заявок → уведомления → COMMIT.
    /// </summary>
    public async Task<ApplicationDto> AcceptAsync(long applicationId, long userId, CancellationToken ct)
    {
        var projectId = await ProjectIdOfAsync(applicationId, ct);
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var application = await db.Applications
            .Include(a => a.Project).Include(a => a.ProjectRole)
            .SingleAsync(a => a.Id == applicationId, ct);
        var project = application.Project;
        var role = application.ProjectRole;

        EnsureOwner(project, userId);
        EnsurePending(application);
        if (project.Status != ProjectStatus.Recruiting)
            throw AppException.Conflict("PROJECT_NOT_RECRUITING", "Принимать заявки можно только во время набора");
        if (role.ClosedAt is not null)
            throw AppException.Conflict("ROLE_CLOSED", "Набор на эту роль закрыт");
        if (await db.Memberships.AnyAsync(m => m.ProjectId == projectId && m.UserId == application.UserId, ct))
            throw AppException.Conflict("ALREADY_MEMBER", "Кандидат уже состоит в команде проекта");

        var occupied = await OccupiedAsync(role.Id, ct);
        if (occupied >= role.Capacity)
            throw AppException.Conflict("ROLE_FULL", "Все места на этой роли уже заняты");

        var now = clock.GetUtcNow();
        application.Status = ApplicationStatus.Accepted;
        application.DecidedAt = now;

        var membership = new Membership
        {
            UserId = application.UserId, ProjectId = projectId, ProjectRoleId = role.Id, JoinedAt = now,
        };
        db.Memberships.Add(membership);
        notifications.Add(application.UserId, NotificationType.ApplicationAccepted,
            $"Вас приняли в проект «{project.Title}» на роль {role.Title}", projectId);

        // Остальные ожидающие заявки этого кандидата в проекте больше не нужны.
        var otherOfCandidate = await db.Applications
            .Where(a => a.ProjectId == projectId && a.UserId == application.UserId
                        && a.Id != application.Id && a.Status == ApplicationStatus.Pending)
            .ToListAsync(ct);
        ApplicationRules.Close(otherOfCandidate, ApplicationCloseReason.AcceptedAnotherRole, project.Title, now, notifications);

        // Если это было последнее место — закрываем ожидающие заявки других кандидатов на роль.
        if (occupied + 1 >= role.Capacity)
        {
            var othersOnRole = await db.Applications
                .Where(a => a.ProjectRoleId == role.Id && a.Id != application.Id && a.Status == ApplicationStatus.Pending)
                .ToListAsync(ct);
            ApplicationRules.Close(othersOnRole, ApplicationCloseReason.RoleFilled, project.Title, now, notifications);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(application, membership.Id);
    }

    private async Task<long> ProjectIdOfAsync(long applicationId, CancellationToken ct) =>
        await db.Applications.Where(a => a.Id == applicationId).Select(a => (long?)a.ProjectId).SingleOrDefaultAsync(ct)
        ?? throw AppException.NotFound("Заявка не найдена");

    private Task<int> OccupiedAsync(long roleId, CancellationToken ct) =>
        db.Memberships.CountAsync(m => m.ProjectRoleId == roleId && m.EndedAt == null, ct);

    private async Task<string> DisplayNameAsync(long userId, CancellationToken ct) =>
        await db.Profiles.Where(p => p.UserId == userId).Select(p => p.DisplayName).SingleOrDefaultAsync(ct) ?? "Кандидат";

    private static void EnsureOwner(Project project, long userId)
    {
        if (project.OwnerId != userId)
            throw AppException.Forbidden("Обрабатывать заявки может только владелец проекта");
    }

    private static void EnsurePending(Application application)
    {
        if (ApplicationRules.IsFinal(application.Status))
            throw AppException.Conflict("APPLICATION_NOT_PENDING",
                $"Заявка уже обработана (статус {application.Status}), действие недоступно");
    }

    private static ApplicationDto ToDto(Application a, long? membershipId) => new(
        a.Id, a.ProjectId, a.ProjectRoleId, a.Status, a.ClosureReason, a.Message, a.CreatedAt, a.DecidedAt, membershipId);
}

using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Data;

public sealed class TeamFinderDbContext(DbContextOptions<TeamFinderDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<RoleType> RoleTypes => Set<RoleType>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<UserRoleInterest> UserRoleInterests => Set<UserRoleInterest>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectRole> ProjectRoles => Set<ProjectRole>();
    public DbSet<RoleSkillRequirement> RoleSkillRequirements => Set<RoleSkillRequirement>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => ConfigureModel(modelBuilder);

    // Also used by the EF Core model snapshot so future migrations compare against this model.
    public static void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Явно фиксируем аннотацию провайдера, используемую в EF Core model snapshot.
        modelBuilder.HasAnnotation(
            "Npgsql:ValueGenerationStrategy",
            Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        });
        modelBuilder.Entity<Profile>(e =>
        {
            e.ToTable("profiles");
            e.HasKey(x => x.UserId);
            e.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.User).WithOne(x => x.Profile).HasForeignKey<Profile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Skill>(e =>
        {
            e.ToTable("skills"); e.HasKey(x => x.Id); e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired(); e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new Skill { Id=1, Name="C#", Description="Язык программирования" }, new Skill { Id=2, Name="ASP.NET Core", Description="Платформа разработки серверных приложений" },
                new Skill { Id=3, Name="PostgreSQL", Description="Реляционная база данных" }, new Skill { Id=4, Name="Entity Framework Core", Description="ORM для .NET" },
                new Skill { Id=5, Name="JavaScript", Description="Язык программирования" }, new Skill { Id=6, Name="TypeScript", Description="Типизированный JavaScript" },
                new Skill { Id=7, Name="React", Description="Библиотека пользовательских интерфейсов" }, new Skill { Id=8, Name="HTML/CSS", Description="Разметка и стилизация веб-страниц" },
                new Skill { Id=9, Name="Docker", Description="Контейнеризация приложений" }, new Skill { Id=10, Name="Git", Description="Контроль версий" },
                new Skill { Id=11, Name="Go", Description="Язык программирования" }, new Skill { Id=12, Name="Redis", Description="Хранилище данных в памяти" },
                new Skill { Id=13, Name="Figma", Description="Инструмент проектирования интерфейсов" }, new Skill { Id=14, Name="Python", Description="Язык программирования" },
                new Skill { Id=15, Name="REST API", Description="Проектирование и разработка API" }, new Skill { Id=16, Name="SQL", Description="Язык запросов к реляционным БД" },
                new Skill { Id=17, Name="Тестирование ПО", Description="Проверка качества программного обеспечения" }, new Skill { Id=18, Name="UI/UX", Description="Проектирование пользовательского опыта" }
            );
        });
        modelBuilder.Entity<RoleType>(e =>
        {
            e.ToTable("role_types"); e.HasKey(x => x.Id); e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired(); e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new RoleType { Id=1, Name="Backend Developer", Description="Разработчик серверной части" }, new RoleType { Id=2, Name="Frontend Developer", Description="Разработчик пользовательского интерфейса" },
                new RoleType { Id=3, Name="Fullstack Developer", Description="Разработчик серверной и клиентской частей" }, new RoleType { Id=4, Name="Designer", Description="Дизайнер" },
                new RoleType { Id=5, Name="DevOps Engineer", Description="Инженер инфраструктуры и развёртывания" }, new RoleType { Id=6, Name="QA Engineer", Description="Инженер по тестированию" },
                new RoleType { Id=7, Name="Product Manager", Description="Менеджер продукта" }, new RoleType { Id=8, Name="Data Analyst", Description="Аналитик данных" }
            );
        });
        modelBuilder.Entity<UserSkill>(e =>
        {
            e.ToTable("user_skills", t => t.HasCheckConstraint("CK_user_skills_level", "\"Level\" IN ('Beginner','Intermediate','Advanced')"));
            e.HasKey(x => new { x.UserId, x.SkillId });
            e.Property(x => x.Level).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.User).WithMany(x => x.Skills).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Skill).WithMany(x => x.Users).HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<UserRoleInterest>(e =>
        {
            e.ToTable("user_role_interests"); e.HasKey(x => new { x.UserId, x.RoleTypeId });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.User).WithMany(x => x.RoleInterests).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RoleType).WithMany(x => x.InterestedUsers).HasForeignKey(x => x.RoleTypeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("projects", t =>
            {
                t.HasCheckConstraint("CK_projects_status", "\"Status\" IN ('Draft','Recruiting','InProgress','Completed','Cancelled')");
                t.HasCheckConstraint("CK_projects_project_type", "\"ProjectType\" IN ('PetProject','Hackathon','Educational')");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired(); e.Property(x => x.Description).IsRequired();
            e.Property(x => x.ProjectType).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.Owner).WithMany(x => x.OwnedProjects).HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProjectRole>(e =>
        {
            e.ToTable("project_roles", t => t.HasCheckConstraint("CK_project_roles_capacity", "\"Capacity\" > 0"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.Title).HasMaxLength(150).IsRequired(); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasAlternateKey(x => new { x.Id, x.ProjectId });
            e.HasOne(x => x.Project).WithMany(x => x.Roles).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RoleType).WithMany(x => x.ProjectRoles).HasForeignKey(x => x.RoleTypeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RoleSkillRequirement>(e =>
        {
            e.ToTable("role_skill_requirements", t =>
            {
                t.HasCheckConstraint("CK_role_skill_requirements_type", "\"RequirementType\" IN ('Required','NiceToHave')");
                t.HasCheckConstraint("CK_role_skill_requirements_minimum_level", "\"MinimumLevel\" IS NULL OR \"MinimumLevel\" IN ('Beginner','Intermediate','Advanced')");
            });
            e.HasKey(x => new { x.ProjectRoleId, x.SkillId });
            e.Property(x => x.RequirementType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MinimumLevel).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.ProjectRole).WithMany(x => x.SkillRequirements).HasForeignKey(x => x.ProjectRoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Skill).WithMany(x => x.RoleRequirements).HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Application>(e =>
        {
            e.ToTable("applications", t =>
            {
                t.HasCheckConstraint("CK_applications_status", "\"Status\" IN ('Pending','Accepted','Rejected','Withdrawn','Closed')");
                t.HasCheckConstraint("CK_applications_closure_reason", "\"ClosureReason\" IS NULL OR \"ClosureReason\" IN ('AcceptedAnotherRole','RoleFilled','RecruitmentClosed','ProjectCancelled')");
                t.HasCheckConstraint("CK_applications_closed_reason", "(\"Status\" = 'Closed' AND \"ClosureReason\" IS NOT NULL) OR (\"Status\" <> 'Closed' AND \"ClosureReason\" IS NULL)");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ClosureReason).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.UserId, x.ProjectRoleId }).IsUnique();
            e.HasOne(x => x.User).WithMany(x => x.Applications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany(x => x.Applications).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProjectRole).WithMany(x => x.Applications).HasForeignKey(x => new { x.ProjectRoleId, x.ProjectId }).HasPrincipalKey(x => new { x.Id, x.ProjectId }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Membership>(e =>
        {
            e.ToTable("memberships", t =>
            {
                t.HasCheckConstraint("CK_memberships_end_reason", "\"EndReason\" IS NULL OR \"EndReason\" IN ('Left','Removed')");
                t.HasCheckConstraint("CK_memberships_end_state", "(\"EndedAt\" IS NULL AND \"EndReason\" IS NULL) OR (\"EndedAt\" IS NOT NULL AND \"EndReason\" IS NOT NULL)");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Id).UseIdentityByDefaultColumn();
            e.Property(x => x.JoinedAt).HasDefaultValueSql("now()");
            e.Property(x => x.EndReason).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.UserId, x.ProjectId }).IsUnique();
            e.HasOne(x => x.User).WithMany(x => x.Memberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany(x => x.Memberships).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProjectRole).WithMany(x => x.Memberships).HasForeignKey(x => new { x.ProjectRoleId, x.ProjectId }).HasPrincipalKey(x => new { x.Id, x.ProjectId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

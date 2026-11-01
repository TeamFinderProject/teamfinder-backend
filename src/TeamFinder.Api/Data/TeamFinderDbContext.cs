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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TelegramLink> TelegramLinks => Set<TelegramLink>();
    public DbSet<TelegramLinkCode> TelegramLinkCodes => Set<TelegramLinkCode>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Все перечисления хранятся строками: в БД видно "Recruiting", а не 1.
        configurationBuilder.Properties<ProjectType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ProjectStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ApplicationStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ApplicationCloseReason>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<SkillLevel>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<RequirementType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<MembershipEndReason>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        const string levels = "'Beginner','Intermediate','Advanced'";

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
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
            e.Property(x => x.Bio).HasMaxLength(2000);
            e.Property(x => x.GitHubUrl).HasMaxLength(500);
            e.Property(x => x.PortfolioUrl).HasMaxLength(500);
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.User).WithOne(x => x.Profile)
                .HasForeignKey<Profile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Skill>(e =>
        {
            e.ToTable("skills");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(SeedData.Skills);
        });

        modelBuilder.Entity<RoleType>(e =>
        {
            e.ToTable("role_types");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(SeedData.RoleTypes);
        });

        modelBuilder.Entity<UserSkill>(e =>
        {
            e.ToTable("user_skills", t => t.HasCheckConstraint("CK_user_skills_level", $"\"Level\" IN ({levels})"));
            e.HasKey(x => new { x.UserId, x.SkillId });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.User).WithMany(x => x.Skills).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserRoleInterest>(e =>
        {
            e.ToTable("user_role_interests");
            e.HasKey(x => new { x.UserId, x.RoleTypeId });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.User).WithMany(x => x.RoleInterests).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RoleType).WithMany().HasForeignKey(x => x.RoleTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("projects", t =>
            {
                t.HasCheckConstraint("CK_projects_status",
                    "\"Status\" IN ('Draft','Recruiting','InProgress','Completed','Cancelled')");
                t.HasCheckConstraint("CK_projects_project_type",
                    "\"ProjectType\" IN ('PetProject','Hackathon','StudyProject')");
            });
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(5000).IsRequired();
            e.Property(x => x.ExternalContactUrl).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.Ignore(x => x.IsFinished);
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Owner).WithMany(x => x.OwnedProjects).HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectRole>(e =>
        {
            e.ToTable("project_roles", t =>
                t.HasCheckConstraint("CK_project_roles_capacity", "\"Capacity\" BETWEEN 1 AND 50"));
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            // Нужен для составных внешних ключей: заявка и участие ссылаются на роль именно своего проекта.
            e.HasAlternateKey(x => new { x.Id, x.ProjectId });
            e.HasOne(x => x.Project).WithMany(x => x.Roles).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RoleType).WithMany().HasForeignKey(x => x.RoleTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoleSkillRequirement>(e =>
        {
            e.ToTable("role_skill_requirements", t =>
            {
                t.HasCheckConstraint("CK_role_skill_requirements_type", "\"RequirementType\" IN ('Required','NiceToHave')");
                t.HasCheckConstraint("CK_role_skill_requirements_minimum_level",
                    $"\"MinimumLevel\" IS NULL OR \"MinimumLevel\" IN ({levels})");
            });
            e.HasKey(x => new { x.ProjectRoleId, x.SkillId });
            e.Property(x => x.MinimumLevel).HasConversion<string>().HasMaxLength(20);
            // Роль удаляется только пока на неё нет заявок, поэтому требования можно удалять вместе с ней.
            e.HasOne(x => x.ProjectRole).WithMany(x => x.SkillRequirements)
                .HasForeignKey(x => x.ProjectRoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Application>(e =>
        {
            e.ToTable("applications", t =>
            {
                t.HasCheckConstraint("CK_applications_status",
                    "\"Status\" IN ('Pending','Accepted','Rejected','Withdrawn','Closed')");
                t.HasCheckConstraint("CK_applications_closure_reason",
                    "\"ClosureReason\" IS NULL OR \"ClosureReason\" IN ('AcceptedAnotherRole','RoleFilled','RoleClosed','RecruitmentClosed','ProjectCancelled')");
                t.HasCheckConstraint("CK_applications_closed_has_reason",
                    "(\"Status\" = 'Closed') = (\"ClosureReason\" IS NOT NULL)");
            });
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.ClosureReason).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            // Одна заявка пользователя на роль за всё время (RULE-APP-02).
            e.HasIndex(x => new { x.UserId, x.ProjectRoleId }).IsUnique();
            e.HasIndex(x => new { x.ProjectId, x.Status });
            e.HasOne(x => x.User).WithMany(x => x.Applications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany(x => x.Applications).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProjectRole).WithMany(x => x.Applications)
                .HasForeignKey(x => new { x.ProjectRoleId, x.ProjectId })
                .HasPrincipalKey(x => new { x.Id, x.ProjectId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Membership>(e =>
        {
            e.ToTable("memberships", t =>
            {
                t.HasCheckConstraint("CK_memberships_end_reason", "\"EndReason\" IS NULL OR \"EndReason\" IN ('Left','Removed')");
                t.HasCheckConstraint("CK_memberships_end_state", "(\"EndedAt\" IS NULL) = (\"EndReason\" IS NULL)");
            });
            e.Property(x => x.JoinedAt).HasDefaultValueSql("now()");
            e.Property(x => x.EndReason).HasConversion<string>().HasMaxLength(20);
            // Не более одного участия пользователя в проекте за всё время (RULE-TEAM-05).
            e.HasIndex(x => new { x.UserId, x.ProjectId }).IsUnique();
            e.HasOne(x => x.User).WithMany(x => x.Memberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany(x => x.Memberships).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProjectRole).WithMany(x => x.Memberships)
                .HasForeignKey(x => new { x.ProjectRoleId, x.ProjectId })
                .HasPrincipalKey(x => new { x.Id, x.ProjectId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.FamilyId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.Property(x => x.Text).HasMaxLength(500).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.UserId, x.ReadAt });
            e.HasIndex(x => x.TelegramSentAt);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TelegramLink>(e =>
        {
            e.ToTable("telegram_links");
            e.HasKey(x => x.UserId);
            // Один чат — один аккаунт.
            e.HasIndex(x => x.ChatId).IsUnique();
            e.HasOne(x => x.User).WithOne().HasForeignKey<TelegramLink>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TelegramLinkCode>(e =>
        {
            e.ToTable("telegram_link_codes");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(32);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

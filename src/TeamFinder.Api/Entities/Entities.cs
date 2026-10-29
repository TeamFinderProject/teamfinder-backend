namespace TeamFinder.Api.Entities;

// В БД перечисления хранятся строками с именами из C# (Recruiting, RoleFilled ...).
// В JSON API те же значения отдаются в UPPER_SNAKE_CASE (RECRUITING, ROLE_FILLED) — см. Program.cs.

public enum ProjectType { PetProject, Hackathon, StudyProject }
public enum ProjectStatus { Draft, Recruiting, InProgress, Completed, Cancelled }
public enum ApplicationStatus { Pending, Accepted, Rejected, Withdrawn, Closed }
public enum ApplicationCloseReason { AcceptedAnotherRole, RoleFilled, RoleClosed, RecruitmentClosed, ProjectCancelled }
public enum SkillLevel { Beginner, Intermediate, Advanced }
public enum RequirementType { Required, NiceToHave }
public enum MembershipEndReason { Left, Removed }

public enum NotificationType
{
    ApplicationReceived,
    ApplicationAccepted,
    ApplicationRejected,
    ApplicationClosed,
    MemberLeft,
    MemberRemoved,
    ProjectStarted,
    ProjectCompleted,
    ProjectCancelled,
}

public sealed class User
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public Profile? Profile { get; set; }
    public ICollection<UserSkill> Skills { get; set; } = new List<UserSkill>();
    public ICollection<UserRoleInterest> RoleInterests { get; set; } = new List<UserRoleInterest>();
    public ICollection<Project> OwnedProjects { get; set; } = new List<Project>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}

public sealed class Profile
{
    public long UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }
    public string? GitHubUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}

public sealed class Skill
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class RoleType
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class UserSkill
{
    public long UserId { get; set; }
    public long SkillId { get; set; }
    public SkillLevel Level { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public User User { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}

public sealed class UserRoleInterest
{
    public long UserId { get; set; }
    public long RoleTypeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public User User { get; set; } = null!;
    public RoleType RoleType { get; set; } = null!;
}

public sealed class Project
{
    public long Id { get; set; }
    public long OwnerId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public ProjectType ProjectType { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;
    public string? ExternalContactUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public User Owner { get; set; } = null!;
    public ICollection<ProjectRole> Roles { get; set; } = new List<ProjectRole>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();

    /// <summary>Проект в конечном состоянии: ничего, кроме просмотра, не разрешено.</summary>
    public bool IsFinished => Status is ProjectStatus.Completed or ProjectStatus.Cancelled;
}

public sealed class ProjectRole
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long RoleTypeId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Project Project { get; set; } = null!;
    public RoleType RoleType { get; set; } = null!;
    public ICollection<RoleSkillRequirement> SkillRequirements { get; set; } = new List<RoleSkillRequirement>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}

public sealed class RoleSkillRequirement
{
    public long ProjectRoleId { get; set; }
    public long SkillId { get; set; }
    public RequirementType RequirementType { get; set; }
    public SkillLevel? MinimumLevel { get; set; }
    public ProjectRole ProjectRole { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}

public sealed class Application
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ProjectId { get; set; }
    public long ProjectRoleId { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public ApplicationCloseReason? ClosureReason { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public User User { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public ProjectRole ProjectRole { get; set; } = null!;
}

public sealed class Membership
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ProjectId { get; set; }
    public long ProjectRoleId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public MembershipEndReason? EndReason { get; set; }
    public User User { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public ProjectRole ProjectRole { get; set; } = null!;
}

public sealed class RefreshToken
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public Guid FamilyId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public long? ReplacedByTokenId { get; set; }
    public User User { get; set; } = null!;
}

public sealed class Notification
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Text { get; set; } = "";
    public long? ProjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>Когда уведомление ушло в Telegram. null — ещё не отправлено (или Telegram не подключён).</summary>
    public DateTimeOffset? TelegramSentAt { get; set; }

    /// <summary>Сколько раз не удалось отправить в Telegram. После 5 попыток больше не пытаемся.</summary>
    public int TelegramAttempts { get; set; }

    public User User { get; set; } = null!;
    public Project? Project { get; set; }
}

/// <summary>Привязка аккаунта TeamFinder к чату с ботом в Telegram.</summary>
public sealed class TelegramLink
{
    public long UserId { get; set; }
    public long ChatId { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
    public User User { get; set; } = null!;
}

/// <summary>Одноразовый код из ссылки t.me/&lt;бот&gt;?start=&lt;код&gt;. Живёт 10 минут.</summary>
public sealed class TelegramLinkCode
{
    public string Code { get; set; } = "";
    public long UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public User User { get; set; } = null!;
}

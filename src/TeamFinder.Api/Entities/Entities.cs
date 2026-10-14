namespace TeamFinder.Api.Entities;

public enum ProjectStatus { Draft, Recruiting, InProgress, Completed, Cancelled }
public enum ApplicationStatus { Pending, Accepted, Rejected, Withdrawn, Closed }
public enum ApplicationClosureReason { AcceptedAnotherRole, RoleFilled, RecruitmentClosed, ProjectCancelled }
public enum ProjectType { PetProject, Hackathon, Educational }
public enum SkillLevel { Beginner, Intermediate, Advanced }
public enum RequirementType { Required, NiceToHave }
public enum MembershipEndReason { Left, Removed }

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
    public ICollection<UserSkill> Users { get; set; } = new List<UserSkill>();
    public ICollection<RoleSkillRequirement> RoleRequirements { get; set; } = new List<RoleSkillRequirement>();
}

public sealed class RoleType
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public ICollection<UserRoleInterest> InterestedUsers { get; set; } = new List<UserRoleInterest>();
    public ICollection<ProjectRole> ProjectRoles { get; set; } = new List<ProjectRole>();
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
}

public sealed class ProjectRole
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long RoleTypeId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int Capacity { get; set; }
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
    public ApplicationClosureReason? ClosureReason { get; set; }
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

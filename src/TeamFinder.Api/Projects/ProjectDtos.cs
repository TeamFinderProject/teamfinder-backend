using System.ComponentModel.DataAnnotations;
using TeamFinder.Api.Common;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Projects;

public enum ProjectRelation { Owner, Member, None }

// ---------- запросы ----------

public sealed class RequirementInput
{
    [Range(1, long.MaxValue, ErrorMessage = "Укажите навык")]
    public long SkillId { get; init; }

    [Required(ErrorMessage = "Укажите, обязательный навык или желательный")]
    public RequirementType? Type { get; init; }

    public SkillLevel? MinimumLevel { get; init; }
}

/// <summary>Роль проекта: при создании проекта (в массиве roles) и при добавлении в существующий проект.</summary>
public sealed class RoleInput
{
    [Range(1, long.MaxValue, ErrorMessage = "Выберите тип роли")]
    public long RoleTypeId { get; init; }

    [MaxLength(150, ErrorMessage = "Не больше 150 символов")]
    public string? Title { get; init; }

    [MaxLength(2000, ErrorMessage = "Не больше 2000 символов")]
    public string? Description { get; init; }

    [Range(1, 50, ErrorMessage = "Количество мест должно быть от 1 до 50")]
    public int Capacity { get; init; }

    public List<RequirementInput> Requirements { get; init; } = [];
}

public sealed class CreateProjectRequest
{
    [Required(ErrorMessage = "Укажите название")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Название — от 3 до 200 символов")]
    public string Title { get; init; } = "";

    [Required(ErrorMessage = "Укажите описание")]
    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Описание — от 10 до 5000 символов")]
    public string Description { get; init; } = "";

    [Required(ErrorMessage = "Выберите тип проекта")]
    public ProjectType? Type { get; init; }

    [AbsoluteHttpUrl, MaxLength(500)]
    public string? ExternalContactUrl { get; init; }

    /// <summary>true — сразу открыть набор (нужна хотя бы одна роль).</summary>
    public bool Publish { get; init; }

    [MaxLength(20, ErrorMessage = "Не больше 20 ролей")]
    public List<RoleInput> Roles { get; init; } = [];
}

/// <summary>Частичное изменение: null — поле не меняется. Пустая строка в ExternalContactUrl очищает контакт.</summary>
public sealed class UpdateProjectRequest
{
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Название — от 3 до 200 символов")]
    public string? Title { get; init; }

    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Описание — от 10 до 5000 символов")]
    public string? Description { get; init; }

    public ProjectType? Type { get; init; }

    [AbsoluteHttpUrl, MaxLength(500)]
    public string? ExternalContactUrl { get; init; }
}

/// <summary>Частичное изменение роли: null — поле не меняется.</summary>
public sealed class UpdateRoleRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Выберите тип роли")]
    public long? RoleTypeId { get; init; }

    [MaxLength(150, ErrorMessage = "Не больше 150 символов")]
    public string? Title { get; init; }

    [MaxLength(2000, ErrorMessage = "Не больше 2000 символов")]
    public string? Description { get; init; }

    [Range(1, 50, ErrorMessage = "Количество мест должно быть от 1 до 50")]
    public int? Capacity { get; init; }

    public List<RequirementInput>? Requirements { get; init; }
}

public sealed class CatalogQuery
{
    public string? Q { get; init; }
    // type разбирается отдельно в контроллере: в query приходит "PET_PROJECT" (см. ApiEnum).
    public long? RoleTypeId { get; init; }
    public long? SkillId { get; init; }
    public bool OpenOnly { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 50)]
    public int PageSize { get; init; } = 20;
}

// ---------- ответы ----------

public sealed record PersonRef(long Id, string DisplayName);

public sealed record RoleCardDto(long Id, long RoleTypeId, string Title, int Capacity, int Occupied, bool IsClosed);

public sealed record ProjectCardDto(
    long Id, string Title, string Description, ProjectType Type, ProjectStatus State,
    PersonRef Owner, IReadOnlyList<RoleCardDto> Roles);

public sealed record MyProjectCardDto(
    long Id, string Title, string Description, ProjectType Type, ProjectStatus State,
    PersonRef Owner, IReadOnlyList<RoleCardDto> Roles, ProjectRelation MyRelation,
    int? PendingApplications); // сколько заявок ждут решения; только для владельца, участнику — null

public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record RoleRequirementDto(long SkillId, string SkillName, RequirementType Type, SkillLevel? MinimumLevel);

public sealed record ProjectRoleDto(
    long Id, long RoleTypeId, string RoleTypeName, string Title, string? Description,
    int Capacity, int Occupied, bool IsClosed, bool RequirementsLocked,
    IReadOnlyList<RoleRequirementDto> Requirements);

public sealed record MyApplicationRef(long ApplicationId, long RoleId, ApplicationStatus Status);

public sealed record ProjectDetailsDto(
    long Id, string Title, string Description, ProjectType Type, ProjectStatus State,
    string? ExternalContactUrl, PersonRef Owner, DateTimeOffset CreatedAt,
    ProjectRelation MyRelation, IReadOnlyList<MyApplicationRef> MyApplications,
    IReadOnlyList<ProjectRoleDto> Roles);

public sealed record ProjectStateDto(long Id, ProjectStatus State);

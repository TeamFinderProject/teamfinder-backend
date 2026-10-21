using System.ComponentModel.DataAnnotations;
using TeamFinder.Api.Common;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Profiles;

public sealed record DictionaryItem(long Id, string Name);

public sealed record UserSkillDto(long SkillId, string Name, SkillLevel Level);

public sealed record ProfileDto(
    long Id,
    string Email,
    string DisplayName,
    string? Bio,
    string? GithubUrl,
    string? PortfolioUrl,
    IReadOnlyList<DictionaryItem> InterestedRoleTypes,
    IReadOnlyList<UserSkillDto> Skills);

public sealed class SkillInput
{
    [Range(1, long.MaxValue, ErrorMessage = "Укажите навык")]
    public long SkillId { get; init; }

    [Required(ErrorMessage = "Укажите уровень")]
    public SkillLevel? Level { get; init; }
}

public sealed class UpdateProfileRequest
{
    [Required(ErrorMessage = "Укажите имя")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя должно быть от 2 до 100 символов")]
    public string DisplayName { get; init; } = "";

    [MaxLength(2000, ErrorMessage = "Не больше 2000 символов")]
    public string? Bio { get; init; }

    [AbsoluteHttpUrl, MaxLength(500)]
    public string? GithubUrl { get; init; }

    [AbsoluteHttpUrl, MaxLength(500)]
    public string? PortfolioUrl { get; init; }

    public List<long> InterestedRoleTypeIds { get; init; } = [];

    public List<SkillInput> Skills { get; init; } = [];
}

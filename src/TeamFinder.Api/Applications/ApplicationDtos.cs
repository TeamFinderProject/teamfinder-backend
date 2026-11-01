using System.ComponentModel.DataAnnotations;
using TeamFinder.Api.Entities;
using TeamFinder.Api.Profiles;
using TeamFinder.Api.Projects;

namespace TeamFinder.Api.Applications;

public sealed class ApplyRequest
{
    [MaxLength(1000, ErrorMessage = "Комментарий — не больше 1000 символов")]
    public string? Message { get; init; }
}

public sealed record ApplicationDto(
    long Id, long ProjectId, long RoleId, ApplicationStatus Status, ApplicationCloseReason? CloseReason,
    string? Message, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt, long? MembershipId);

public sealed record ProjectRefDto(long Id, string Title, ProjectStatus State);

public sealed record RoleRefDto(long Id, string Title);

public sealed record MyApplicationDto(
    long Id, ProjectRefDto Project, RoleRefDto Role, ApplicationStatus Status, ApplicationCloseReason? CloseReason,
    string? Message, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt, bool CanWithdraw);

public sealed record CandidateDto(
    long Id, string DisplayName, string? Bio, string? GithubUrl,
    IReadOnlyList<UserSkillDto> Skills, IReadOnlyList<DictionaryItem> InterestedRoleTypes);

public sealed record ProjectApplicationDto(
    long Id, long RoleId, string RoleTitle, ApplicationStatus Status, ApplicationCloseReason? CloseReason,
    string? Message, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt,
    CandidateDto Candidate, MatchSummaryDto Match);

using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Common;
using TeamFinder.Api.Data;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Projects;

public enum MatchStatus { Matched, LevelTooLow, Missing }

public sealed record MatchItemDto(long SkillId, string Skill, SkillLevel? MinimumLevel, SkillLevel? UserLevel, MatchStatus Status);

public sealed record MatchSummaryDto(int RequiredMatched, int RequiredTotal, int NiceToHaveMatched, int NiceToHaveTotal);

public sealed record MatchResultDto(
    long RoleId,
    bool ProfileHasSkills,
    bool RoleTypeMatchesInterest,
    IReadOnlyList<MatchItemDto> Required,
    IReadOnlyList<MatchItemDto> NiceToHave,
    MatchSummaryDto Summary);

/// <summary>
/// Объяснимый matching (RULE-MATCH-01…06): по каждому навыку роли — есть ли он у пользователя
/// и достаточен ли уровень. Без процентов и весов. Результат ни на что не влияет автоматически.
/// </summary>
public sealed class MatchingService(TeamFinderDbContext db)
{
    public async Task<MatchResultDto> GetForCurrentUserAsync(long userId, long projectId, long roleId, CancellationToken ct)
    {
        var role = await db.ProjectRoles.AsNoTracking()
            .Include(r => r.Project)
            .Include(r => r.SkillRequirements).ThenInclude(s => s.Skill)
            .SingleOrDefaultAsync(r => r.Id == roleId && r.ProjectId == projectId, ct)
            ?? throw AppException.NotFound("Роль не найдена");

        if (role.Project.Status == ProjectStatus.Draft && role.Project.OwnerId != userId)
            throw AppException.NotFound("Роль не найдена");

        var userSkills = await db.UserSkills.AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.SkillId, s => s.Level, ct);
        var interested = await db.UserRoleInterests.AnyAsync(i => i.UserId == userId && i.RoleTypeId == role.RoleTypeId, ct);

        return Compute(role, userSkills, interested);
    }

    public static MatchResultDto Compute(ProjectRole role, IReadOnlyDictionary<long, SkillLevel> userSkills, bool roleTypeMatchesInterest)
    {
        var items = role.SkillRequirements
            .OrderBy(r => r.Skill.Name)
            .Select(r =>
            {
                SkillLevel? userLevel = userSkills.TryGetValue(r.SkillId, out var level) ? level : null;
                var status = userLevel is null ? MatchStatus.Missing
                    : r.MinimumLevel is not null && userLevel < r.MinimumLevel ? MatchStatus.LevelTooLow
                    : MatchStatus.Matched;
                return (r.RequirementType, Item: new MatchItemDto(r.SkillId, r.Skill.Name, r.MinimumLevel, userLevel, status));
            })
            .ToList();

        var required = items.Where(i => i.RequirementType == RequirementType.Required).Select(i => i.Item).ToList();
        var niceToHave = items.Where(i => i.RequirementType == RequirementType.NiceToHave).Select(i => i.Item).ToList();

        return new MatchResultDto(
            role.Id,
            userSkills.Count > 0,
            roleTypeMatchesInterest,
            required,
            niceToHave,
            Summarize(required, niceToHave));
    }

    private static MatchSummaryDto Summarize(IReadOnlyList<MatchItemDto> required, IReadOnlyList<MatchItemDto> niceToHave) => new(
        required.Count(i => i.Status == MatchStatus.Matched), required.Count,
        niceToHave.Count(i => i.Status == MatchStatus.Matched), niceToHave.Count);
}

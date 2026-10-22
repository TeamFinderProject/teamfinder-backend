using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Data;

namespace TeamFinder.Api.Common;

/// <summary>Проверка, что id из запроса есть в справочниках (RULE-PROF-01, RULE-ROLE-04).</summary>
public static class DictionaryGuard
{
    public static async Task EnsureSkillsExistAsync(TeamFinderDbContext db, IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return;
        var found = await db.Skills.CountAsync(s => distinct.Contains(s.Id), ct);
        if (found != distinct.Count)
            throw AppException.BadRequest("UNKNOWN_SKILL", "Навык не найден в справочнике");
    }

    public static async Task EnsureRoleTypesExistAsync(TeamFinderDbContext db, IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return;
        var found = await db.RoleTypes.CountAsync(r => distinct.Contains(r.Id), ct);
        if (found != distinct.Count)
            throw AppException.BadRequest("UNKNOWN_ROLE_TYPE", "Тип роли не найден в справочнике");
    }
}

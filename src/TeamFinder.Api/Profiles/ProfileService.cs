using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Common;
using TeamFinder.Api.Data;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Profiles;

public sealed class ProfileService(TeamFinderDbContext db, TimeProvider clock)
{
    public async Task<ProfileDto> GetAsync(long userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.Profile)
            .Include(u => u.Skills).ThenInclude(s => s.Skill)
            .Include(u => u.RoleInterests).ThenInclude(r => r.RoleType)
            .SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw AppException.NotFound("Пользователь не найден");

        return ToDto(user);
    }

    /// <summary>Сохранение формы профиля целиком: списки навыков и интересов заменяются.</summary>
    public async Task<ProfileDto> UpdateAsync(long userId, UpdateProfileRequest request, CancellationToken ct)
    {
        if (request.Skills.Select(s => s.SkillId).Distinct().Count() != request.Skills.Count)
            throw AppException.BadRequest("DUPLICATE_SKILL", "Навык указан в профиле несколько раз");

        var skillIds = request.Skills.Select(s => s.SkillId).ToList();
        var roleTypeIds = request.InterestedRoleTypeIds.Distinct().ToList();
        await DictionaryGuard.EnsureSkillsExistAsync(db, skillIds, ct);
        await DictionaryGuard.EnsureRoleTypesExistAsync(db, roleTypeIds, ct);

        var user = await db.Users
            .Include(u => u.Profile)
            .Include(u => u.Skills)
            .Include(u => u.RoleInterests)
            .SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw AppException.NotFound("Пользователь не найден");

        var now = clock.GetUtcNow();
        var profile = user.Profile ??= new Profile { UserId = userId };
        profile.DisplayName = request.DisplayName.Trim();
        profile.Bio = Blank(request.Bio);
        profile.GitHubUrl = Blank(request.GithubUrl);
        profile.PortfolioUrl = Blank(request.PortfolioUrl);
        profile.UpdatedAt = now;

        // Навыки: удаляем лишние, обновляем уровень у оставшихся, добавляем новые.
        var wanted = request.Skills.ToDictionary(s => s.SkillId, s => s.Level!.Value);
        foreach (var existing in user.Skills.ToList())
        {
            if (wanted.TryGetValue(existing.SkillId, out var level))
                existing.Level = level;
            else
                db.UserSkills.Remove(existing);
        }
        foreach (var (skillId, level) in wanted.Where(w => user.Skills.All(s => s.SkillId != w.Key)))
            db.UserSkills.Add(new UserSkill { UserId = userId, SkillId = skillId, Level = level, CreatedAt = now });

        foreach (var existing in user.RoleInterests.Where(r => !roleTypeIds.Contains(r.RoleTypeId)).ToList())
            db.UserRoleInterests.Remove(existing);
        foreach (var roleTypeId in roleTypeIds.Where(id => user.RoleInterests.All(r => r.RoleTypeId != id)))
            db.UserRoleInterests.Add(new UserRoleInterest { UserId = userId, RoleTypeId = roleTypeId, CreatedAt = now });

        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    internal static ProfileDto ToDto(User user) => new(
        user.Id,
        user.Email,
        user.Profile?.DisplayName ?? "",
        user.Profile?.Bio,
        user.Profile?.GitHubUrl,
        user.Profile?.PortfolioUrl,
        user.RoleInterests.OrderBy(r => r.RoleType.Name)
            .Select(r => new DictionaryItem(r.RoleTypeId, r.RoleType.Name)).ToList(),
        user.Skills.OrderBy(s => s.Skill.Name)
            .Select(s => new UserSkillDto(s.SkillId, s.Skill.Name, s.Level)).ToList());

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Data;
using TeamFinder.Api.Profiles;

namespace TeamFinder.Api.Dictionaries;

/// <summary>Справочники доступны и гостю: они нужны ещё до входа (например, на лендинге и при регистрации).</summary>
[ApiController]
[AllowAnonymous]
[Route("api")]
[Produces("application/json")]
public sealed class DictionariesController(TeamFinderDbContext db) : ControllerBase
{
    [HttpGet("skills")]
    public async Task<IReadOnlyList<DictionaryItem>> Skills(CancellationToken ct) =>
        await db.Skills.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new DictionaryItem(s.Id, s.Name)).ToListAsync(ct);

    [HttpGet("role-types")]
    public async Task<IReadOnlyList<DictionaryItem>> RoleTypes(CancellationToken ct) =>
        await db.RoleTypes.AsNoTracking().OrderBy(r => r.Id)
            .Select(r => new DictionaryItem(r.Id, r.Name)).ToListAsync(ct);
}

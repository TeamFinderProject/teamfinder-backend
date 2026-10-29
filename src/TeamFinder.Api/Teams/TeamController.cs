using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamFinder.Api.Common;

namespace TeamFinder.Api.Teams;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:long}")]
[Produces("application/json")]
public sealed class TeamController(TeamService teams) : ControllerBase
{
    [HttpGet("team")]
    public Task<TeamDto> Get(long projectId, CancellationToken ct) => teams.GetAsync(projectId, User.GetUserId(), ct);

    [HttpPost("team/leave")]
    public async Task<IActionResult> Leave(long projectId, CancellationToken ct)
    {
        await teams.LeaveAsync(projectId, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Исключить участника. projectId и userId — в пути: это адрес подресурса «участник проекта».
    /// У DELETE нет тела с определённой семантикой (RFC 9110, 9.3.5), поэтому id в тело не переносим.
    /// </summary>
    [HttpDelete("members/{userId:long}")]
    public async Task<IActionResult> Remove(long projectId, long userId, CancellationToken ct)
    {
        await teams.RemoveAsync(projectId, userId, User.GetUserId(), ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamFinder.Api.Common;

namespace TeamFinder.Api.Projects;

/// <summary>Роли — подресурс проекта: /api/projects/{projectId}/roles/{roleId}.</summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:long}/roles")]
[Produces("application/json")]
public sealed class ProjectRolesController(ProjectService projects, MatchingService matching) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ProjectRoleDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectRoleDto>> Add(long projectId, RoleInput request, CancellationToken ct)
    {
        var role = await projects.AddRoleAsync(projectId, User.GetUserId(), request, ct);
        return StatusCode(StatusCodes.Status201Created, role);
    }

    [HttpPatch("{roleId:long}")]
    public Task<ProjectRoleDto> Update(long projectId, long roleId, UpdateRoleRequest request, CancellationToken ct) =>
        projects.UpdateRoleAsync(projectId, roleId, User.GetUserId(), request, ct);

    [HttpPost("{roleId:long}/close")]
    public Task<ProjectRoleDto> Close(long projectId, long roleId, CancellationToken ct) =>
        projects.CloseRoleAsync(projectId, roleId, User.GetUserId(), ct);

    [HttpDelete("{roleId:long}")]
    public async Task<IActionResult> Delete(long projectId, long roleId, CancellationToken ct)
    {
        await projects.DeleteRoleAsync(projectId, roleId, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpGet("{roleId:long}/match")]
    public Task<MatchResultDto> Match(long projectId, long roleId, CancellationToken ct) =>
        matching.GetForCurrentUserAsync(User.GetUserId(), projectId, roleId, ct);
}

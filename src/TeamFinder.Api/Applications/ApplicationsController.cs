using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamFinder.Api.Common;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Applications;

[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class ApplicationsController(ApplicationService applications) : ControllerBase
{
    /// <summary>Заявка подаётся на конкретную роль конкретного проекта.</summary>
    [HttpPost("api/projects/{projectId:long}/roles/{roleId:long}/applications")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApplicationDto>> Apply(
        long projectId, long roleId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ApplyRequest? request,
        CancellationToken ct)
    {
        var created = await applications.ApplyAsync(User.GetUserId(), projectId, roleId, request ?? new ApplyRequest(), ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet("api/applications/mine")]
    public Task<IReadOnlyList<MyApplicationDto>> Mine(CancellationToken ct) => applications.GetMineAsync(User.GetUserId(), ct);

    [HttpGet("api/projects/{projectId:long}/applications")]
    public Task<IReadOnlyList<ProjectApplicationDto>> ForProject(
        long projectId, [FromQuery] string? status, [FromQuery] long? roleId, CancellationToken ct) =>
        applications.GetForProjectAsync(projectId, User.GetUserId(),
            ApiEnum.ParseOptional<ApplicationStatus>(status, "status"), roleId, ct);

    [HttpPost("api/applications/{applicationId:long}/withdraw")]
    public Task<ApplicationDto> Withdraw(long applicationId, CancellationToken ct) =>
        applications.WithdrawAsync(applicationId, User.GetUserId(), ct);

    [HttpPost("api/applications/{applicationId:long}/accept")]
    public Task<ApplicationDto> Accept(long applicationId, CancellationToken ct) =>
        applications.AcceptAsync(applicationId, User.GetUserId(), ct);

    [HttpPost("api/applications/{applicationId:long}/reject")]
    public Task<ApplicationDto> Reject(long applicationId, CancellationToken ct) =>
        applications.RejectAsync(applicationId, User.GetUserId(), ct);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamFinder.Api.Common;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Projects;

[ApiController]
[Authorize]
[Route("api/projects")]
[Produces("application/json")]
public sealed class ProjectsController(ProjectService projects) : ControllerBase
{
    /// <summary>Каталог: фильтры — в query-параметрах (?type=HACKATHON&amp;skillId=3), объект по id — путём.</summary>
    [HttpGet]
    public Task<PageDto<ProjectCardDto>> Catalog([FromQuery] CatalogQuery query, [FromQuery] string? type, CancellationToken ct) =>
        projects.GetCatalogAsync(query, ApiEnum.ParseOptional<ProjectType>(type, "type"), ct);

    [HttpGet("mine")]
    public Task<IReadOnlyList<MyProjectCardDto>> Mine(CancellationToken ct) => projects.GetMineAsync(User.GetUserId(), ct);

    [HttpGet("{projectId:long}")]
    public Task<ProjectDetailsDto> Get(long projectId, CancellationToken ct) =>
        projects.GetDetailsAsync(projectId, User.GetUserId(), ct);

    /// <summary>Создать проект вместе с ролями и требованиями к навыкам — одним запросом и одной транзакцией.</summary>
    [HttpPost]
    [ProducesResponseType<ProjectDetailsDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectDetailsDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var created = await projects.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { projectId = created.Id }, created);
    }

    [HttpPatch("{projectId:long}")]
    public Task<ProjectDetailsDto> Update(long projectId, UpdateProjectRequest request, CancellationToken ct) =>
        projects.UpdateAsync(projectId, User.GetUserId(), request, ct);

    [HttpPost("{projectId:long}/publish")]
    public Task<ProjectStateDto> Publish(long projectId, CancellationToken ct) => projects.PublishAsync(projectId, User.GetUserId(), ct);

    [HttpPost("{projectId:long}/start")]
    public Task<ProjectStateDto> Start(long projectId, CancellationToken ct) => projects.StartAsync(projectId, User.GetUserId(), ct);

    [HttpPost("{projectId:long}/complete")]
    public Task<ProjectStateDto> Complete(long projectId, CancellationToken ct) => projects.CompleteAsync(projectId, User.GetUserId(), ct);

    [HttpPost("{projectId:long}/cancel")]
    public Task<ProjectStateDto> Cancel(long projectId, CancellationToken ct) => projects.CancelAsync(projectId, User.GetUserId(), ct);
}

using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Applications;
using TeamFinder.Api.Common;
using TeamFinder.Api.Data;
using TeamFinder.Api.Entities;
using TeamFinder.Api.Notifications;

namespace TeamFinder.Api.Projects;

/// <summary>Проекты, их роли и жизненный цикл.</summary>
public sealed class ProjectService(TeamFinderDbContext db, NotificationService notifications, TimeProvider clock)
{
    // ================= Чтение =================

    public async Task<PageDto<ProjectCardDto>> GetCatalogAsync(CatalogQuery query, ProjectType? type, CancellationToken ct)
    {
        // Черновики в каталоге не показываются (RULE-PROJ-02).
        var projects = db.Projects.AsNoTracking().Where(p => p.Status != ProjectStatus.Draft);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = $"%{query.Q.Trim()}%";
            projects = projects.Where(p => EF.Functions.ILike(p.Title, pattern) || EF.Functions.ILike(p.Description, pattern));
        }
        if (type is not null)
            projects = projects.Where(p => p.ProjectType == type);
        if (query.RoleTypeId is not null)
            projects = projects.Where(p => p.Roles.Any(r => r.RoleTypeId == query.RoleTypeId));
        if (query.SkillId is not null)
            projects = projects.Where(p => p.Roles.Any(r => r.SkillRequirements.Any(s => s.SkillId == query.SkillId)));
        if (query.OpenOnly)
            projects = projects.Where(p => p.Status == ProjectStatus.Recruiting && p.Roles.Any(r =>
                r.ClosedAt == null && r.Memberships.Count(m => m.EndedAt == null) < r.Capacity));

        var total = await projects.CountAsync(ct);
        var items = await projects
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProjectCardDto(
                p.Id, p.Title, p.Description, p.ProjectType, p.Status,
                new PersonRef(p.OwnerId, p.Owner.Profile!.DisplayName),
                p.Roles.OrderBy(r => r.Id).Select(r => new RoleCardDto(
                    r.Id, r.RoleTypeId, r.Title, r.Capacity,
                    r.Memberships.Count(m => m.EndedAt == null), r.ClosedAt != null)).ToList()))
            .ToListAsync(ct);

        return new PageDto<ProjectCardDto>(items, query.Page, query.PageSize, total);
    }

    /// <summary>Проекты, где пользователь владелец (включая черновики) или действующий участник.</summary>
    public async Task<IReadOnlyList<MyProjectCardDto>> GetMineAsync(long userId, CancellationToken ct) =>
        await db.Projects.AsNoTracking()
            .Where(p => p.OwnerId == userId || p.Memberships.Any(m => m.UserId == userId && m.EndedAt == null))
            .OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.Id)
            .Select(p => new MyProjectCardDto(
                p.Id, p.Title, p.Description, p.ProjectType, p.Status,
                new PersonRef(p.OwnerId, p.Owner.Profile!.DisplayName),
                p.Roles.OrderBy(r => r.Id).Select(r => new RoleCardDto(
                    r.Id, r.RoleTypeId, r.Title, r.Capacity,
                    r.Memberships.Count(m => m.EndedAt == null), r.ClosedAt != null)).ToList(),
                p.OwnerId == userId ? ProjectRelation.Owner : ProjectRelation.Member,
                p.OwnerId == userId ? (int?)p.Applications.Count(a => a.Status == ApplicationStatus.Pending) : null))
            .ToListAsync(ct);

    public async Task<ProjectDetailsDto> GetDetailsAsync(long projectId, long userId, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking()
            .Include(p => p.Owner).ThenInclude(o => o.Profile)
            .Include(p => p.Roles).ThenInclude(r => r.RoleType)
            .Include(p => p.Roles).ThenInclude(r => r.SkillRequirements).ThenInclude(s => s.Skill)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw AppException.NotFound("Проект не найден");

        var isOwner = project.OwnerId == userId;
        if (project.Status == ProjectStatus.Draft && !isOwner)
            throw AppException.NotFound("Проект не найден");

        var occupied = await OccupiedByRoleAsync(projectId, ct);
        var rolesWithApplications = await db.Applications.AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .Select(a => a.ProjectRoleId).Distinct().ToListAsync(ct);
        var isMember = await db.Memberships.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId && m.EndedAt == null, ct);
        var myApplications = await db.Applications.AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.UserId == userId)
            .OrderBy(a => a.Id)
            .Select(a => new MyApplicationRef(a.Id, a.ProjectRoleId, a.Status))
            .ToListAsync(ct);

        return new ProjectDetailsDto(
            project.Id, project.Title, project.Description, project.ProjectType, project.Status,
            // Контакт команды видят только владелец и действующие участники (RULE-TEAM-07).
            isOwner || isMember ? project.ExternalContactUrl : null,
            new PersonRef(project.OwnerId, project.Owner.Profile?.DisplayName ?? ""),
            project.CreatedAt,
            isOwner ? ProjectRelation.Owner : isMember ? ProjectRelation.Member : ProjectRelation.None,
            myApplications,
            project.Roles.OrderBy(r => r.Id)
                .Select(r => ToRoleDto(r, occupied.GetValueOrDefault(r.Id), rolesWithApplications.Contains(r.Id)))
                .ToList());
    }

    // ================= Создание и изменение проекта =================

    /// <summary>
    /// Проект создаётся вместе с ролями и требованиями одним SaveChanges, то есть одной транзакцией:
    /// либо сохраняется всё, либо ничего (RULE-PROJ-01).
    /// </summary>
    public async Task<ProjectDetailsDto> CreateAsync(long ownerId, CreateProjectRequest request, CancellationToken ct)
    {
        for (var i = 0; i < request.Roles.Count; i++)
            EnsureNoDuplicateSkills(request.Roles[i].Requirements, $"roles[{i}].requirements");

        if (request.Publish && request.Roles.Count == 0)
            throw AppException.Conflict("PROJECT_HAS_NO_ROLES", "Чтобы открыть набор, добавьте хотя бы одну роль");

        var roleTypeNames = await LoadRoleTypeNamesAsync(request.Roles.Select(r => r.RoleTypeId), ct);
        await DictionaryGuard.EnsureSkillsExistAsync(
            db, request.Roles.SelectMany(r => r.Requirements).Select(q => q.SkillId).ToList(), ct);

        var now = clock.GetUtcNow();
        var project = new Project
        {
            OwnerId = ownerId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            ProjectType = request.Type!.Value,
            ExternalContactUrl = Blank(request.ExternalContactUrl),
            Status = request.Publish ? ProjectStatus.Recruiting : ProjectStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };
        foreach (var input in request.Roles)
            project.Roles.Add(BuildRole(input, roleTypeNames, now));

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return await GetDetailsAsync(project.Id, ownerId, ct);
    }

    public async Task<ProjectDetailsDto> UpdateAsync(long projectId, long userId, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await LoadOwnedAsync(projectId, userId, ct);
        EnsureEditable(project);

        if (request.Type is not null && request.Type != project.ProjectType)
        {
            if (project.Status != ProjectStatus.Draft)
                throw AppException.Conflict("PROJECT_NOT_EDITABLE", "Тип проекта можно изменить только в черновике");
            project.ProjectType = request.Type.Value;
        }
        if (request.Title is not null) project.Title = request.Title.Trim();
        if (request.Description is not null) project.Description = request.Description.Trim();
        if (request.ExternalContactUrl is not null) project.ExternalContactUrl = Blank(request.ExternalContactUrl);
        project.UpdatedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(ct);
        return await GetDetailsAsync(projectId, userId, ct);
    }

    // ================= Жизненный цикл (раздел 7.5 контракта) =================

    public async Task<ProjectStateDto> PublishAsync(long projectId, long userId, CancellationToken ct)
    {
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var project = await LoadOwnedAsync(projectId, userId, ct);
        EnsureTransition(project, ProjectStatus.Draft);

        if (!await db.ProjectRoles.AnyAsync(r => r.ProjectId == projectId && r.ClosedAt == null, ct))
            throw AppException.Conflict("PROJECT_HAS_NO_ROLES", "Чтобы открыть набор, добавьте хотя бы одну роль");

        project.Status = ProjectStatus.Recruiting;
        project.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new ProjectStateDto(project.Id, project.Status);
    }

    public async Task<ProjectStateDto> StartAsync(long projectId, long userId, CancellationToken ct)
    {
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var project = await LoadOwnedAsync(projectId, userId, ct);
        EnsureTransition(project, ProjectStatus.Recruiting);

        project.Status = ProjectStatus.InProgress;
        project.UpdatedAt = clock.GetUtcNow();
        await ClosePendingAsync(project, ApplicationCloseReason.RecruitmentClosed, ct);
        await NotifyMembersAsync(project, NotificationType.ProjectStarted, $"Проект «{project.Title}» переведён в работу", ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new ProjectStateDto(project.Id, project.Status);
    }

    public async Task<ProjectStateDto> CompleteAsync(long projectId, long userId, CancellationToken ct)
    {
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var project = await LoadOwnedAsync(projectId, userId, ct);
        EnsureTransition(project, ProjectStatus.InProgress);

        project.Status = ProjectStatus.Completed;
        project.UpdatedAt = clock.GetUtcNow();
        await NotifyMembersAsync(project, NotificationType.ProjectCompleted, $"Проект «{project.Title}» завершён", ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new ProjectStateDto(project.Id, project.Status);
    }

    public async Task<ProjectStateDto> CancelAsync(long projectId, long userId, CancellationToken ct)
    {
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var project = await LoadOwnedAsync(projectId, userId, ct);
        EnsureTransition(project, ProjectStatus.Draft, ProjectStatus.Recruiting, ProjectStatus.InProgress);

        project.Status = ProjectStatus.Cancelled;
        project.UpdatedAt = clock.GetUtcNow();
        await ClosePendingAsync(project, ApplicationCloseReason.ProjectCancelled, ct);
        await NotifyMembersAsync(project, NotificationType.ProjectCancelled, $"Проект «{project.Title}» отменён", ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new ProjectStateDto(project.Id, project.Status);
    }

    // ================= Роли (раздел 8 контракта) =================

    public async Task<ProjectRoleDto> AddRoleAsync(long projectId, long userId, RoleInput input, CancellationToken ct)
    {
        EnsureNoDuplicateSkills(input.Requirements, "requirements");
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var project = await LoadOwnedAsync(projectId, userId, ct);
        if (project.Status is not (ProjectStatus.Draft or ProjectStatus.Recruiting))
            throw AppException.Conflict("PROJECT_NOT_EDITABLE", "Роли можно добавлять только в черновике или во время набора");

        var roleTypeNames = await LoadRoleTypeNamesAsync([input.RoleTypeId], ct);
        await DictionaryGuard.EnsureSkillsExistAsync(db, input.Requirements.Select(r => r.SkillId).ToList(), ct);

        var now = clock.GetUtcNow();
        var role = BuildRole(input, roleTypeNames, now);
        role.ProjectId = projectId;
        db.ProjectRoles.Add(role);
        project.UpdatedAt = now;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetRoleAsync(projectId, role.Id, ct);
    }

    public async Task<ProjectRoleDto> UpdateRoleAsync(long projectId, long roleId, long userId, UpdateRoleRequest request, CancellationToken ct)
    {
        if (request.Requirements is not null)
            EnsureNoDuplicateSkills(request.Requirements, "requirements");

        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var project = await LoadOwnedAsync(projectId, userId, ct);
        EnsureEditable(project);
        var role = await db.ProjectRoles
            .Include(r => r.SkillRequirements)
            .SingleOrDefaultAsync(r => r.Id == roleId && r.ProjectId == projectId, ct)
            ?? throw AppException.NotFound("Роль не найдена");

        var hasApplications = await db.Applications.AnyAsync(a => a.ProjectRoleId == roleId, ct);
        var typeChanges = request.RoleTypeId is not null && request.RoleTypeId != role.RoleTypeId;

        // После первой заявки тип и навыки менять нельзя: кандидат подавался на конкретные требования (RULE-ROLE-06).
        if (hasApplications && (typeChanges || request.Requirements is not null))
            throw AppException.Conflict("ROLE_LOCKED",
                "На роль уже подавали заявки: тип роли и навыки изменить нельзя. Закройте роль и создайте новую");

        if (typeChanges)
        {
            var names = await LoadRoleTypeNamesAsync([request.RoleTypeId!.Value], ct);
            var oldName = await db.RoleTypes.Where(t => t.Id == role.RoleTypeId).Select(t => t.Name).SingleAsync(ct);
            if (role.Title == oldName) role.Title = names[request.RoleTypeId.Value];
            role.RoleTypeId = request.RoleTypeId.Value;
        }

        if (request.Requirements is not null)
        {
            await DictionaryGuard.EnsureSkillsExistAsync(db, request.Requirements.Select(r => r.SkillId).ToList(), ct);
            db.RoleSkillRequirements.RemoveRange(role.SkillRequirements);
            foreach (var r in request.Requirements)
                db.RoleSkillRequirements.Add(new RoleSkillRequirement
                {
                    ProjectRoleId = role.Id, SkillId = r.SkillId, RequirementType = r.Type!.Value, MinimumLevel = r.MinimumLevel,
                });
        }

        if (request.Title is not null)
            role.Title = string.IsNullOrWhiteSpace(request.Title)
                ? await db.RoleTypes.Where(t => t.Id == role.RoleTypeId).Select(t => t.Name).SingleAsync(ct)
                : request.Title.Trim();
        if (request.Description is not null)
            role.Description = Blank(request.Description);

        if (request.Capacity is not null)
        {
            var occupied = await db.Memberships.CountAsync(m => m.ProjectRoleId == roleId && m.EndedAt == null, ct);
            if (request.Capacity < occupied)
                throw AppException.Conflict("CAPACITY_BELOW_OCCUPIED",
                    $"В роли уже {occupied} участник(ов): количество мест не может быть меньше");
            role.Capacity = request.Capacity.Value;
            if (role.Capacity == occupied)
                await ClosePendingForRoleAsync(project, role, ApplicationCloseReason.RoleFilled, ct);
        }

        project.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetRoleAsync(projectId, roleId, ct);
    }

    /// <summary>Закрыть набор на роль: новые заявки не принимаются, ожидающие закрываются (RULE-ROLE-09).</summary>
    public async Task<ProjectRoleDto> CloseRoleAsync(long projectId, long roleId, long userId, CancellationToken ct)
    {
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var project = await LoadOwnedAsync(projectId, userId, ct);
        EnsureEditable(project);
        var role = await db.ProjectRoles.SingleOrDefaultAsync(r => r.Id == roleId && r.ProjectId == projectId, ct)
            ?? throw AppException.NotFound("Роль не найдена");

        if (role.ClosedAt is null)
        {
            role.ClosedAt = clock.GetUtcNow();
            await ClosePendingForRoleAsync(project, role, ApplicationCloseReason.RoleClosed, ct);
            project.UpdatedAt = role.ClosedAt.Value;
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return await GetRoleAsync(projectId, roleId, ct);
    }

    /// <summary>Удалить можно только роль без заявок и участников, иначе — закрыть (RULE-ROLE-07).</summary>
    public async Task DeleteRoleAsync(long projectId, long roleId, long userId, CancellationToken ct)
    {
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var project = await LoadOwnedAsync(projectId, userId, ct);
        EnsureEditable(project);
        var role = await db.ProjectRoles.SingleOrDefaultAsync(r => r.Id == roleId && r.ProjectId == projectId, ct)
            ?? throw AppException.NotFound("Роль не найдена");

        if (await db.Applications.AnyAsync(a => a.ProjectRoleId == roleId, ct)
            || await db.Memberships.AnyAsync(m => m.ProjectRoleId == roleId, ct))
            throw AppException.Conflict("ROLE_HAS_APPLICATIONS",
                "На роль уже подавали заявки, поэтому её нельзя удалить — только закрыть");

        db.ProjectRoles.Remove(role);
        project.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // ================= Вспомогательное =================

    internal async Task<ProjectRoleDto> GetRoleAsync(long projectId, long roleId, CancellationToken ct)
    {
        var role = await db.ProjectRoles.AsNoTracking()
            .Include(r => r.RoleType)
            .Include(r => r.SkillRequirements).ThenInclude(s => s.Skill)
            .SingleOrDefaultAsync(r => r.Id == roleId && r.ProjectId == projectId, ct)
            ?? throw AppException.NotFound("Роль не найдена");
        var occupied = await db.Memberships.CountAsync(m => m.ProjectRoleId == roleId && m.EndedAt == null, ct);
        var locked = await db.Applications.AnyAsync(a => a.ProjectRoleId == roleId, ct);
        return ToRoleDto(role, occupied, locked);
    }

    private static ProjectRoleDto ToRoleDto(ProjectRole r, int occupied, bool requirementsLocked) => new(
        r.Id, r.RoleTypeId, r.RoleType.Name, r.Title, r.Description, r.Capacity, occupied,
        r.ClosedAt is not null, requirementsLocked,
        r.SkillRequirements
            .OrderBy(s => s.RequirementType).ThenBy(s => s.Skill.Name)
            .Select(s => new RoleRequirementDto(s.SkillId, s.Skill.Name, s.RequirementType, s.MinimumLevel))
            .ToList());

    private async Task<Dictionary<long, int>> OccupiedByRoleAsync(long projectId, CancellationToken ct) =>
        await db.Memberships.AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.EndedAt == null)
            .GroupBy(m => m.ProjectRoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

    private async Task<Project> LoadOwnedAsync(long projectId, long userId, CancellationToken ct)
    {
        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw AppException.NotFound("Проект не найден");
        if (project.OwnerId != userId)
        {
            // Чужой черновик для постороннего не существует; опубликованный — существует, но прав нет.
            if (project.Status == ProjectStatus.Draft) throw AppException.NotFound("Проект не найден");
            throw AppException.Forbidden("Управлять проектом может только его владелец");
        }
        return project;
    }

    private static void EnsureEditable(Project project)
    {
        if (project.IsFinished)
            throw AppException.Conflict("PROJECT_NOT_EDITABLE", "Проект завершён или отменён, изменения недоступны");
    }

    private static void EnsureTransition(Project project, params ProjectStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(project.Status))
            throw AppException.Conflict("INVALID_STATE_TRANSITION",
                $"Действие недоступно: проект {StateName(project.Status)}");
    }

    private static void EnsureNoDuplicateSkills(IReadOnlyCollection<RequirementInput> requirements, string field)
    {
        if (requirements.Select(r => r.SkillId).Distinct().Count() != requirements.Count)
            throw new AppException(StatusCodes.Status400BadRequest, "DUPLICATE_SKILL",
                "Навык указан в роли несколько раз (один навык не может быть одновременно обязательным и желательным)",
                new Dictionary<string, string[]> { [field] = ["Навык повторяется"] });
    }

    private async Task<Dictionary<long, string>> LoadRoleTypeNamesAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var names = await db.RoleTypes.AsNoTracking()
            .Where(t => distinct.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        if (names.Count != distinct.Count)
            throw AppException.BadRequest("UNKNOWN_ROLE_TYPE", "Тип роли не найден в справочнике");
        return names;
    }

    private static ProjectRole BuildRole(RoleInput input, IReadOnlyDictionary<long, string> roleTypeNames, DateTimeOffset now)
    {
        var role = new ProjectRole
        {
            RoleTypeId = input.RoleTypeId,
            Title = string.IsNullOrWhiteSpace(input.Title) ? roleTypeNames[input.RoleTypeId] : input.Title.Trim(),
            Description = Blank(input.Description),
            Capacity = input.Capacity,
            CreatedAt = now,
        };
        foreach (var r in input.Requirements)
            role.SkillRequirements.Add(new RoleSkillRequirement
            {
                SkillId = r.SkillId, RequirementType = r.Type!.Value, MinimumLevel = r.MinimumLevel,
            });
        return role;
    }

    /// <summary>Закрывает все ожидающие заявки проекта (переход в работу, отмена — RULE-APP-07).</summary>
    private async Task ClosePendingAsync(Project project, ApplicationCloseReason reason, CancellationToken ct)
    {
        var pending = await db.Applications
            .Where(a => a.ProjectId == project.Id && a.Status == ApplicationStatus.Pending)
            .ToListAsync(ct);
        ApplicationRules.Close(pending, reason, project.Title, clock.GetUtcNow(), notifications);
    }

    private async Task ClosePendingForRoleAsync(Project project, ProjectRole role, ApplicationCloseReason reason, CancellationToken ct)
    {
        var pending = await db.Applications
            .Where(a => a.ProjectRoleId == role.Id && a.Status == ApplicationStatus.Pending)
            .ToListAsync(ct);
        ApplicationRules.Close(pending, reason, project.Title, clock.GetUtcNow(), notifications);
    }

    private async Task NotifyMembersAsync(Project project, NotificationType type, string text, CancellationToken ct)
    {
        var memberIds = await db.Memberships
            .Where(m => m.ProjectId == project.Id && m.EndedAt == null)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        foreach (var memberId in memberIds)
            notifications.Add(memberId, type, text, project.Id);
    }

    private static string StateName(ProjectStatus status) => status switch
    {
        ProjectStatus.Draft => "ещё черновик",
        ProjectStatus.Recruiting => "в наборе",
        ProjectStatus.InProgress => "уже в работе",
        ProjectStatus.Completed => "завершён",
        ProjectStatus.Cancelled => "отменён",
        _ => status.ToString(),
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Common;
using TeamFinder.Api.Data;
using TeamFinder.Api.Entities;
using TeamFinder.Api.Notifications;
using TeamFinder.Api.Projects;

namespace TeamFinder.Api.Teams;

public sealed record TeamProjectDto(long Id, string Title, ProjectType Type, ProjectStatus State);

public sealed record TeamMemberDto(long MembershipId, long UserId, string DisplayName, long RoleId, string RoleTitle, DateTimeOffset JoinedAt);

public sealed record TeamDto(TeamProjectDto Project, PersonRef Owner, string? ExternalContactUrl, IReadOnlyList<TeamMemberDto> Members);

/// <summary>
/// Состав команды. Участие не удаляется: выход и исключение заполняют EndedAt и EndReason,
/// место в роли освобождается, история сохраняется (RULE-TEAM-02…04).
/// </summary>
public sealed class TeamService(TeamFinderDbContext db, NotificationService notifications, TimeProvider clock)
{
    public async Task<TeamDto> GetAsync(long projectId, long userId, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking()
            .Include(p => p.Owner).ThenInclude(o => o.Profile)
            .SingleOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw AppException.NotFound("Проект не найден");

        var members = await db.Memberships.AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.EndedAt == null)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new TeamMemberDto(m.Id, m.UserId, m.User.Profile!.DisplayName, m.ProjectRoleId, m.ProjectRole.Title, m.JoinedAt))
            .ToListAsync(ct);

        if (project.OwnerId != userId && members.All(m => m.UserId != userId))
            throw AppException.Forbidden("Страница команды доступна только владельцу и участникам");

        return new TeamDto(
            new TeamProjectDto(project.Id, project.Title, project.ProjectType, project.Status),
            new PersonRef(project.OwnerId, project.Owner.Profile?.DisplayName ?? ""),
            project.ExternalContactUrl,
            members);
    }

    public async Task LeaveAsync(long projectId, long userId, CancellationToken ct)
    {
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw AppException.NotFound("Проект не найден");
        EnsureNotFinished(project);

        var membership = await db.Memberships
            .Include(m => m.User).ThenInclude(u => u.Profile)
            .SingleOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId && m.EndedAt == null, ct)
            ?? throw AppException.Conflict("NOT_A_MEMBER", "Вы не состоите в команде этого проекта");

        End(membership, MembershipEndReason.Left);
        notifications.Add(project.OwnerId, NotificationType.MemberLeft,
            $"{membership.User.Profile?.DisplayName ?? "Участник"} больше не в команде проекта «{project.Title}»", projectId);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>DELETE /api/projects/{projectId}/members/{userId}: участник адресуется путём, тела у DELETE нет.</summary>
    public async Task RemoveAsync(long projectId, long memberUserId, long ownerId, CancellationToken ct)
    {
        await using var tx = await db.BeginProjectLockAsync(projectId, ct);
        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw AppException.NotFound("Проект не найден");
        if (project.OwnerId != ownerId)
            throw AppException.Forbidden("Исключать участников может только владелец проекта");
        EnsureNotFinished(project);

        var membership = await db.Memberships
            .SingleOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == memberUserId && m.EndedAt == null, ct)
            ?? throw AppException.NotFound("Участник не найден в команде");

        End(membership, MembershipEndReason.Removed);
        notifications.Add(memberUserId, NotificationType.MemberRemoved,
            $"Владелец исключил вас из команды проекта «{project.Title}»", projectId);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private void End(Membership membership, MembershipEndReason reason)
    {
        membership.EndedAt = clock.GetUtcNow();
        membership.EndReason = reason;
    }

    private static void EnsureNotFinished(Project project)
    {
        if (project.IsFinished)
            throw AppException.Conflict("PROJECT_NOT_EDITABLE", "Проект завершён или отменён, состав команды не меняется");
    }
}

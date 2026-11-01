using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Common;
using TeamFinder.Api.Data;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Notifications;

public sealed record NotificationDto(long Id, NotificationType Type, string Text, long? ProjectId, DateTimeOffset CreatedAt, bool IsRead);

/// <summary>
/// Уведомления. Add только добавляет запись в текущий DbContext: она сохраняется тем же SaveChanges,
/// что и само событие (transactional outbox, RULE-NOTIF-01). Отправка в Telegram и почту в v1 будет
/// фоновой задачей, которая читает эту таблицу, — сервисы заявок и проектов при этом не меняются.
/// </summary>
public sealed class NotificationService(TeamFinderDbContext db, TimeProvider clock)
{
    public void Add(long userId, NotificationType type, string text, long? projectId) =>
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Text = text.Length > 500 ? text[..500] : text,
            ProjectId = projectId,
            CreatedAt = clock.GetUtcNow(),
        });

    public async Task<IReadOnlyList<NotificationDto>> GetAsync(long userId, bool unreadOnly, CancellationToken ct) =>
        await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && (!unreadOnly || n.ReadAt == null))
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(50)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Text, n.ProjectId, n.CreatedAt, n.ReadAt != null))
            .ToListAsync(ct);

    public async Task MarkReadAsync(long userId, long notificationId, CancellationToken ct)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct)
            ?? throw AppException.NotFound("Уведомление не найдено");
        notification.ReadAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(long userId, CancellationToken ct)
    {
        DateTimeOffset? now = clock.GetUtcNow();
        await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }
}

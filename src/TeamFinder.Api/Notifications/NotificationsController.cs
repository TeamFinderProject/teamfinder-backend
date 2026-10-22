using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamFinder.Api.Common;

namespace TeamFinder.Api.Notifications;

[ApiController]
[Authorize]
[Route("api/notifications")]
[Produces("application/json")]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<NotificationDto>> Get([FromQuery] bool unreadOnly, CancellationToken ct) =>
        notifications.GetAsync(User.GetUserId(), unreadOnly, ct);

    [HttpPost("{notificationId:long}/read")]
    public async Task<IActionResult> MarkRead(long notificationId, CancellationToken ct)
    {
        await notifications.MarkReadAsync(User.GetUserId(), notificationId, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(User.GetUserId(), ct);
        return NoContent();
    }
}

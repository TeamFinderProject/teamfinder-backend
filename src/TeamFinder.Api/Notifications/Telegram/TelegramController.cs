using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamFinder.Api.Common;

namespace TeamFinder.Api.Notifications.Telegram;

/// <summary>Подключение Telegram-уведомлений в профиле (раздел 12.1 контракта).</summary>
[ApiController]
[Authorize]
[Route("api/profile/telegram")]
[Produces("application/json")]
public sealed class TelegramController(TelegramService telegram) : ControllerBase
{
    [HttpGet]
    public Task<TelegramStatusDto> Status(CancellationToken ct) => telegram.GetStatusAsync(User.GetUserId(), ct);

    /// <summary>Одноразовая ссылка на бота. Пользователь открывает её и нажимает Start.</summary>
    [HttpPost("link")]
    public Task<TelegramLinkDto> CreateLink(CancellationToken ct) => telegram.CreateLinkCodeAsync(User.GetUserId(), ct);

    /// <summary>Отключить Telegram. Тела нет: удаляется привязка текущего пользователя.</summary>
    [HttpDelete]
    public async Task<IActionResult> Unlink(CancellationToken ct)
    {
        await telegram.UnlinkAsync(User.GetUserId(), ct);
        return NoContent();
    }
}

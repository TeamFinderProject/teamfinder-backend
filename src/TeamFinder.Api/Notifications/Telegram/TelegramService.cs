using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using TeamFinder.Api.Common;
using TeamFinder.Api.Data;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Notifications.Telegram;

public sealed record TelegramStatusDto(bool Enabled, bool Linked, string? BotUsername, DateTimeOffset? LinkedAt);

public sealed record TelegramLinkDto(string Code, string Url, DateTimeOffset ExpiresAt);

/// <summary>
/// Привязка Telegram к аккаунту:
/// 1) пользователь в профиле нажимает «Подключить Telegram» — получает ссылку t.me/&lt;бот&gt;?start=&lt;код&gt;;
/// 2) в Telegram нажимает Start — бот получает «/start &lt;код&gt;»;
/// 3) по коду находим пользователя и запоминаем chat_id. Дальше уведомления уходят в этот чат.
/// </summary>
public sealed class TelegramService(TeamFinderDbContext db, IOptions<TelegramOptions> options, TimeProvider clock)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    public async Task<TelegramStatusDto> GetStatusAsync(long userId, CancellationToken ct)
    {
        var link = await db.TelegramLinks.AsNoTracking().SingleOrDefaultAsync(l => l.UserId == userId, ct);
        return new TelegramStatusDto(options.Value.Enabled, link is not null, BotName(), link?.LinkedAt);
    }

    public async Task<TelegramLinkDto> CreateLinkCodeAsync(long userId, CancellationToken ct)
    {
        if (!options.Value.Enabled)
            throw AppException.Conflict("TELEGRAM_DISABLED", "Telegram-бот не настроен на сервере");

        var now = clock.GetUtcNow();
        // Старые и чужие просроченные коды больше не нужны.
        await db.TelegramLinkCodes.Where(c => c.UserId == userId || c.ExpiresAt < now).ExecuteDeleteAsync(ct);

        var code = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(12)); // 16 символов [A-Za-z0-9_-]
        var entity = new TelegramLinkCode { Code = code, UserId = userId, ExpiresAt = now + CodeLifetime };
        db.TelegramLinkCodes.Add(entity);
        await db.SaveChangesAsync(ct);

        return new TelegramLinkDto(code, $"https://t.me/{BotName()}?start={code}", entity.ExpiresAt);
    }

    public async Task UnlinkAsync(long userId, CancellationToken ct) =>
        await db.TelegramLinks.Where(l => l.UserId == userId).ExecuteDeleteAsync(ct);

    /// <summary>Обработать текст, который пришёл боту. Возвращает ответ, который бот отправит в чат.</summary>
    public async Task<string> HandleMessageAsync(long chatId, string text, CancellationToken ct)
    {
        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts.Length > 0 ? parts[0].Split('@')[0].ToLowerInvariant() : "";

        if (command == "/start" && parts.Length == 2)
            return await LinkAsync(chatId, parts[1].Trim(), ct);

        if (command == "/stop")
        {
            var removed = await db.TelegramLinks.Where(l => l.ChatId == chatId).ExecuteDeleteAsync(ct);
            return removed > 0
                ? "Уведомления TeamFinder в этот чат отключены. Подключить снова можно в профиле на сайте."
                : "Этот чат не подключён к TeamFinder.";
        }

        return "Это бот уведомлений TeamFinder. Чтобы подключить его, откройте профиль на сайте и нажмите «Подключить Telegram». "
               + "Отключить уведомления — команда /stop.";
    }

    private async Task<string> LinkAsync(long chatId, string code, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var linkCode = await db.TelegramLinkCodes.SingleOrDefaultAsync(c => c.Code == code, ct);
        if (linkCode is null || linkCode.ExpiresAt < now)
            return "Ссылка устарела или уже использована. Получите новую в профиле TeamFinder.";

        // Чат можно привязать только к одному аккаунту, а аккаунт — только к одному чату.
        await db.TelegramLinks.Where(l => l.ChatId == chatId || l.UserId == linkCode.UserId).ExecuteDeleteAsync(ct);
        db.TelegramLinks.Add(new TelegramLink { UserId = linkCode.UserId, ChatId = chatId, LinkedAt = now });
        db.TelegramLinkCodes.Remove(linkCode);
        await db.SaveChangesAsync(ct);

        var name = await db.Profiles.Where(p => p.UserId == linkCode.UserId).Select(p => p.DisplayName).SingleOrDefaultAsync(ct);
        return $"Готово, {name}! Уведомления TeamFinder о заявках и команде будут приходить сюда. Отключить — /stop.";
    }

    private string BotName() => options.Value.BotUsername.TrimStart('@');
}

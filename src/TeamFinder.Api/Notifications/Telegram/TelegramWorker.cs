using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TeamFinder.Api.Data;

namespace TeamFinder.Api.Notifications.Telegram;

/// <summary>
/// Фоновая задача внутри процесса API. Делает две вещи параллельно:
/// - читает сообщения боту (long polling) и обрабатывает /start и /stop;
/// - раз в несколько секунд берёт из таблицы notifications то, что ещё не ушло в Telegram, и отправляет.
///
/// Уведомление попадает в таблицу в той же транзакции, что и событие (outbox). Если Telegram недоступен,
/// заявка всё равно принята, а сообщение отправится на следующей попытке (до 5 попыток).
/// </summary>
public sealed class TelegramWorker(
    IServiceScopeFactory scopes,
    TelegramClient telegram,
    IOptions<TelegramOptions> options,
    TimeProvider clock,
    ILogger<TelegramWorker> logger) : BackgroundService
{
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Telegram выключен: Telegram:BotToken не задан");
            return;
        }

        await Task.WhenAll(PollUpdatesAsync(stoppingToken), DispatchLoopAsync(stoppingToken));
    }

    private async Task PollUpdatesAsync(CancellationToken ct)
    {
        long offset = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var update in await telegram.GetUpdatesAsync(offset, timeoutSeconds: 25, ct))
                {
                    offset = update.UpdateId + 1;
                    if (update.ChatId == 0 || string.IsNullOrWhiteSpace(update.Text)) continue;

                    using var scope = scopes.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<TelegramService>();
                    var reply = await service.HandleMessageAsync(update.ChatId, update.Text, ct);
                    await telegram.SendMessageAsync(update.ChatId, reply, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Ошибка чтения сообщений Telegram, повтор через 5 секунд");
                await Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private async Task DispatchLoopAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.DispatchIntervalSeconds));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Ошибка отправки уведомлений в Telegram");
            }
            await Delay(interval, ct);
        }
    }

    private async Task DispatchOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TeamFinderDbContext>();
        var since = clock.GetUtcNow().AddDays(-1);

        // Только уведомления после привязки: не засыпаем человека старыми событиями.
        var batch = await (
                from n in db.Notifications
                join l in db.TelegramLinks on n.UserId equals l.UserId
                where n.TelegramSentAt == null && n.TelegramAttempts < MaxAttempts
                      && n.CreatedAt >= l.LinkedAt && n.CreatedAt >= since
                orderby n.Id
                select new { Notification = n, l.ChatId })
            .Take(50)
            .ToListAsync(ct);

        foreach (var item in batch)
        {
            var sent = await telegram.SendMessageAsync(item.ChatId, Format(item.Notification.Text, item.Notification.ProjectId), ct);
            if (sent) item.Notification.TelegramSentAt = clock.GetUtcNow();
            else item.Notification.TelegramAttempts++;
        }

        if (batch.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private string Format(string text, long? projectId)
    {
        var site = options.Value.SiteUrl.TrimEnd('/');
        return projectId is not null && site.Length > 0
            ? $"TeamFinder: {text}\nОткрыть: {site}/projects/{projectId}"
            : $"TeamFinder: {text}";
    }

    private static async Task Delay(TimeSpan delay, CancellationToken ct)
    {
        try { await Task.Delay(delay, ct); }
        catch (OperationCanceledException) { }
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TeamFinder.Api.Notifications.Telegram;

/// <summary>Входящее сообщение боту: из какого чата и какой текст.</summary>
public sealed record TelegramUpdate(long UpdateId, long ChatId, string Text);

/// <summary>
/// Минимальный клиент Telegram Bot API (https://core.telegram.org/bots/api): два метода — отправить сообщение
/// и получить новые сообщения боту (long polling). Отдельная библиотека не нужна.
/// </summary>
public sealed class TelegramClient(HttpClient http, IOptions<TelegramOptions> options, ILogger<TelegramClient> logger)
{
    private string BaseUrl => $"https://api.telegram.org/bot{options.Value.BotToken}/";

    public async Task<bool> SendMessageAsync(long chatId, string text, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(BaseUrl + "sendMessage",
                new { chat_id = chatId, text, disable_web_page_preview = true }, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Telegram sendMessage failed: {Status}", (int)response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Telegram sendMessage failed");
            return false;
        }
    }

    /// <summary>
    /// getUpdates с ожиданием до timeoutSeconds: Telegram держит запрос открытым, пока не придёт сообщение.
    /// Так бот работает и на локальном компьютере — публичный адрес для webhook не нужен.
    /// </summary>
    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct)
    {
        var url = $"{BaseUrl}getUpdates?offset={offset}&timeout={timeoutSeconds}&allowed_updates=%5B%22message%22%5D";
        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            // 401 — неверный токен; 409 — у бота включён webhook или тот же токен уже читает другой процесс.
            // Бросаем исключение: TelegramWorker подождёт 5 секунд и повторит, а не будет слать запросы без паузы.
            var hint = (int)response.StatusCode switch
            {
                401 => "неверный Telegram:BotToken",
                409 => "у бота включён webhook (вызовите deleteWebhook) или этот токен уже запущен в другом месте",
                _ => "ошибка Telegram",
            };
            throw new HttpRequestException($"Telegram getUpdates failed: {(int)response.StatusCode} ({hint})");
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var updates = new List<TelegramUpdate>();
        foreach (var item in json.RootElement.GetProperty("result").EnumerateArray())
        {
            var updateId = item.GetProperty("update_id").GetInt64();
            if (item.TryGetProperty("message", out var message)
                && message.TryGetProperty("text", out var text)
                && message.TryGetProperty("chat", out var chat))
            {
                updates.Add(new TelegramUpdate(updateId, chat.GetProperty("id").GetInt64(), text.GetString() ?? ""));
            }
            else
            {
                // Не текстовое сообщение — пропускаем, но offset всё равно сдвигаем.
                updates.Add(new TelegramUpdate(updateId, 0, ""));
            }
        }
        return updates;
    }
}

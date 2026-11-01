namespace TeamFinder.Api.Notifications.Telegram;

/// <summary>
/// Настройки Telegram-бота (секция "Telegram"). Если BotToken пустой, Telegram выключен:
/// сайт работает как обычно, уведомления видны только внутри сервиса.
/// Токен выдаёт @BotFather; в репозиторий не коммитим — только через .env / переменную Telegram__BotToken.
/// </summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = "";

    /// <summary>Имя бота без @, например teamfinder_demo_bot. Нужно для ссылки t.me/&lt;имя&gt;?start=&lt;код&gt;.</summary>
    public string BotUsername { get; set; } = "";

    /// <summary>Адрес сайта для ссылки «Открыть проект» в сообщении, например http://localhost:8081.</summary>
    public string SiteUrl { get; set; } = "";

    /// <summary>Как часто проверять неотправленные уведомления.</summary>
    public int DispatchIntervalSeconds { get; set; } = 5;

    public bool Enabled => !string.IsNullOrWhiteSpace(BotToken);
}

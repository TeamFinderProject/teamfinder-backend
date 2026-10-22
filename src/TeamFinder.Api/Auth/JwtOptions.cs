namespace TeamFinder.Api.Auth;

/// <summary>Настройки токенов. Секция "Jwt" в appsettings, в Docker/CI — переменные Jwt__SigningKey и т. д.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "teamfinder";
    public string Audience { get; set; } = "teamfinder-web";

    /// <summary>Ключ подписи HMAC-SHA256, не короче 32 символов. В репозитории — только ключ для разработки.</summary>
    public string SigningKey { get; set; } = "";

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>
    /// Сколько секунд после ротации старый refresh token ещё принимается. Нужно, когда две вкладки
    /// обновляют токен одновременно. После окна повторное использование считается кражей.
    /// </summary>
    public int RefreshReuseGraceSeconds { get; set; } = 30;

    /// <summary>Сколько попыток входа в минуту разрешено с одного IP-адреса.</summary>
    public int LoginAttemptsPerMinute { get; set; } = 10;

    /// <summary>Cookie с флагом Secure. В локальной разработке по http выключается.</summary>
    public bool SecureCookie { get; set; } = true;
}

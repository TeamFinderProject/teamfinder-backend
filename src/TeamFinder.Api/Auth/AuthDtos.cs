using System.ComponentModel.DataAnnotations;

namespace TeamFinder.Api.Auth;

// Тела запросов — классы со свойствами: атрибуты валидации на свойствах однозначно
// подхватываются MVC (у positional record их пришлось бы вешать на параметры конструктора).

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress(ErrorMessage = "Некорректный формат email")]
    [MaxLength(320)]
    public string Email { get; init; } = "";

    [Required(ErrorMessage = "Укажите пароль")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Пароль должен быть от 8 до 128 символов")]
    public string Password { get; init; } = "";

    [Required(ErrorMessage = "Укажите имя")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя должно быть от 2 до 100 символов")]
    public string DisplayName { get; init; } = "";
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Укажите email")]
    public string Email { get; init; } = "";

    [Required(ErrorMessage = "Укажите пароль")]
    public string Password { get; init; } = "";
}

public sealed record UserSummary(long Id, string Email, string DisplayName);

public sealed record AuthResponse(string AccessToken, string TokenType, int ExpiresIn, UserSummary User);

/// <summary>Результат входа: тело ответа + refresh token, который контроллер кладёт в cookie.</summary>
public sealed record AuthResult(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshExpiresAt);

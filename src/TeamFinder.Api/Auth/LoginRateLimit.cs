using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TeamFinder.Api.Common;

namespace TeamFinder.Api.Auth;

/// <summary>
/// Защита от перебора паролей: на POST /api/auth/login не больше Jwt:LoginAttemptsPerMinute запросов
/// в минуту с одного IP-адреса. Лишние запросы получают 429 TOO_MANY_ATTEMPTS.
/// Используется штатный rate limiter ASP.NET Core, отдельные пакеты не нужны.
/// </summary>
public static class LoginRateLimit
{
    public const string PolicyName = "login";

    public static IServiceCollection AddLoginRateLimit(this IServiceCollection services, int attemptsPerMinute) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse("TOO_MANY_ATTEMPTS", "Слишком много попыток входа. Подождите минуту и попробуйте снова"), ct);
            };
            options.AddPolicy(PolicyName, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, attemptsPerMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
}

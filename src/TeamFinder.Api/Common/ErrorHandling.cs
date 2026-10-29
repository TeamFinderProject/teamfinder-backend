using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TeamFinder.Api.Common;

/// <summary>
/// Единая обработка ошибок: AppException → {code, message}, нарушение уникальности в БД → 409,
/// всё остальное → 500 без деталей реализации (NFR-03).
/// </summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, body) = exception switch
        {
            AppException app => (app.StatusCode, new ErrorResponse(app.Code, app.Message, app.Errors)),

            // Гонка, которую не поймала проверка в сервисе: две одинаковые заявки одновременно и т. п.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict,
                    new ErrorResponse("CONFLICT", "Такая запись уже существует. Обновите страницу и повторите действие")),

            _ => (StatusCodes.Status500InternalServerError,
                new ErrorResponse("INTERNAL_ERROR", "Внутренняя ошибка сервера")),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(body, ct);
        return true;
    }

    /// <summary>Ошибки валидации DTO ([Required], [Range] ...) в формате контракта вместо ProblemDetails.</summary>
    public static IActionResult InvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .ToDictionary(
                kv => ToCamelPath(kv.Key),
                kv => kv.Value!.Errors
                    .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Некорректное значение" : e.ErrorMessage)
                    .ToArray());

        return new BadRequestObjectResult(new ErrorResponse("VALIDATION_ERROR", "Некорректные входные данные", errors));
    }

    // "Roles[0].Capacity" -> "roles[0].capacity", "$.type" -> "type"
    private static string ToCamelPath(string key)
    {
        key = key.StartsWith("$.") ? key[2..] : key;
        if (key.Length == 0) return "body";
        var parts = key.Split('.');
        for (var i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0) parts[i] = char.ToLowerInvariant(parts[i][0]) + parts[i][1..];
        return string.Join('.', parts);
    }
}

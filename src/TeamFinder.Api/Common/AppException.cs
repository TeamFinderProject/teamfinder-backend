namespace TeamFinder.Api.Common;

/// <summary>
/// Ошибка бизнес-правила или доступа. Превращается в ответ { code, message, errors }
/// с нужным HTTP-кодом (см. ErrorHandling). Коды — раздел 14 api-contracts-v1.md.
/// </summary>
public sealed class AppException(int statusCode, string code, string message,
    IDictionary<string, string[]>? errors = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static AppException Validation(string field, string message) =>
        new(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Некорректные входные данные",
            new Dictionary<string, string[]> { [field] = [message] });

    public static AppException BadRequest(string code, string message) =>
        new(StatusCodes.Status400BadRequest, code, message);

    public static AppException Unauthorized(string code, string message) =>
        new(StatusCodes.Status401Unauthorized, code, message);

    public static AppException Forbidden(string message = "Недостаточно прав для этого действия") =>
        new(StatusCodes.Status403Forbidden, "FORBIDDEN", message);

    public static AppException NotFound(string message = "Ресурс не найден") =>
        new(StatusCodes.Status404NotFound, "NOT_FOUND", message);

    public static AppException Conflict(string code, string message) =>
        new(StatusCodes.Status409Conflict, code, message);
}

/// <summary>Тело ответа с ошибкой — одинаковое для всех endpoint'ов.</summary>
public sealed record ErrorResponse(string Code, string Message, IDictionary<string, string[]>? Errors = null);

namespace TeamFinder.Api.Common;

/// <summary>
/// Разбор перечислений из query-параметров. В JSON значения уже приходят в UPPER_SNAKE_CASE
/// и разбираются конвертером System.Text.Json, а query-строку разбираем сами: "PET_PROJECT" → PetProject.
/// </summary>
public static class ApiEnum
{
    public static T? ParseOptional<T>(string? value, string parameterName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Replace("_", "", StringComparison.Ordinal);
        if (Enum.TryParse<T>(normalized, ignoreCase: true, out var result) && Enum.IsDefined(result))
            return result;

        throw AppException.Validation(parameterName, $"Недопустимое значение «{value}»");
    }
}

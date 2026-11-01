using System.Security.Claims;

namespace TeamFinder.Api.Common;

public static class CurrentUserExtensions
{
    /// <summary>
    /// Id текущего пользователя из claim "sub" access token'а.
    /// Это единственный источник userId для действий от своего имени (RULE-SEC-03).
    /// </summary>
    public static long GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst("sub")?.Value;
        return long.TryParse(sub, out var id)
            ? id
            : throw AppException.Unauthorized("UNAUTHORIZED", "Требуется вход в систему");
    }
}

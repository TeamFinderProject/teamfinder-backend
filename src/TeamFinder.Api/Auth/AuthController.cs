using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TeamFinder.Api.Common;

namespace TeamFinder.Api.Auth;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(AuthService auth, IOptions<JwtOptions> options) : ControllerBase
{
    public const string RefreshCookieName = "tf_refresh";
    private const string RefreshCookiePath = "/api/auth";

    /// <summary>Регистрация. Сразу выполняет вход: access token в ответе, refresh token в HttpOnly-cookie.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        SetRefreshCookie(result);
        return StatusCode(StatusCodes.Status201Created, result.Response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimit.PolicyName)] // не больше N попыток в минуту с одного адреса
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        SetRefreshCookie(result);
        return Ok(result.Response);
    }

    /// <summary>Новый access token по refresh-cookie. Старый refresh token отзывается (ротация).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        try
        {
            var result = await auth.RefreshAsync(Request.Cookies[RefreshCookieName], ct);
            SetRefreshCookie(result);
            return Ok(result.Response);
        }
        catch
        {
            DeleteRefreshCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookieName], ct);
        DeleteRefreshCookie();
        return NoContent();
    }

    /// <summary>Текущий пользователь по access token. Frontend вызывает его, чтобы узнать, кто вошёл.</summary>
    [HttpGet("me")]
    [Authorize]
    public Task<UserSummary> Me(CancellationToken ct) => auth.GetCurrentUserAsync(User.GetUserId(), ct);

    private void SetRefreshCookie(AuthResult result) =>
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.Value.SecureCookie,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = result.RefreshExpiresAt,
        });

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.Value.SecureCookie,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
        });
}

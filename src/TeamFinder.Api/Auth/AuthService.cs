using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TeamFinder.Api.Common;
using TeamFinder.Api.Data;
using TeamFinder.Api.Entities;

namespace TeamFinder.Api.Auth;

/// <summary>
/// Регистрация, вход, обновление и отзыв токенов. Решение описано в docs/adr-001-authentication.md.
/// </summary>
public sealed class AuthService(
    TeamFinderDbContext db,
    TokenService tokens,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> options,
    TimeProvider clock)
{
    private readonly JwtOptions _options = options.Value;

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw AppException.Conflict("EMAIL_TAKEN", "Пользователь с таким email уже зарегистрирован");

        var now = clock.GetUtcNow();
        var user = new User { Email = email, CreatedAt = now };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        // Пользователь и профиль создаются вместе: профиль 1:1 обязателен после регистрации.
        user.Profile = new Profile { DisplayName = request.DisplayName.Trim(), UpdatedAt = now };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var (result, _) = await IssueTokensAsync(user, Guid.NewGuid(), ct);
        return result;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.Include(u => u.Profile).SingleOrDefaultAsync(u => u.Email == email, ct);

        // Одинаковая ошибка для «нет пользователя» и «неверный пароль»: не подсказываем, что email существует.
        if (user is null)
            throw InvalidCredentials();

        var passwordCheck = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (passwordCheck == PasswordVerificationResult.Failed)
            throw InvalidCredentials();

        if (passwordCheck == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var (result, _) = await IssueTokensAsync(user, Guid.NewGuid(), ct);
        return result;
    }

    /// <summary>
    /// Ротация: предъявленный refresh token отзывается, выдаётся новый в той же цепочке.
    /// Если предъявлен уже отозванный токен — считаем, что его украли, и отзываем всю цепочку.
    /// </summary>
    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken))
            throw InvalidRefreshToken();

        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.Profile)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            throw InvalidRefreshToken();

        var now = clock.GetUtcNow();

        if (stored.RevokedAt is not null)
        {
            // Две вкладки обновили токен почти одновременно: вторая пришла со старым токеном,
            // который первая только что заменила. В течение короткого окна это не кража — выдаём новый.
            var justRotated = stored.ReplacedByTokenId is not null
                              && now - stored.RevokedAt.Value <= TimeSpan.FromSeconds(_options.RefreshReuseGraceSeconds);
            if (!justRotated)
            {
                await RevokeFamilyAsync(stored.FamilyId, now, ct);
                throw InvalidRefreshToken();
            }

            var (graced, _) = await IssueTokensAsync(stored.User, stored.FamilyId, ct);
            return graced;
        }

        if (stored.ExpiresAt <= now)
            throw InvalidRefreshToken();

        stored.RevokedAt = now;
        var (result, replacement) = await IssueTokensAsync(stored.User, stored.FamilyId, ct);

        stored.ReplacedByTokenId = replacement.Id;
        await db.SaveChangesAsync(ct);

        return result;
    }

    public async Task<UserSummary> GetCurrentUserAsync(long userId, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserSummary(u.Id, u.Email, u.Profile!.DisplayName))
            .SingleOrDefaultAsync(ct)
        ?? throw AppException.Unauthorized("UNAUTHORIZED", "Требуется вход в систему");

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return;

        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<(AuthResult Result, RefreshToken Entity)> IssueTokensAsync(
        User user, Guid familyId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var refreshToken = TokenService.GenerateRefreshToken();
        var expiresAt = now.AddDays(_options.RefreshTokenDays);

        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenService.Hash(refreshToken),
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);

        var response = new AuthResponse(
            tokens.CreateAccessToken(user),
            "Bearer",
            tokens.AccessTokenLifetimeSeconds,
            new UserSummary(user.Id, user.Email, user.Profile?.DisplayName ?? ""));

        return (new AuthResult(response, refreshToken, expiresAt), entity);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct)
    {
        var active = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in active)
            token.RevokedAt = now;
        await db.SaveChangesAsync(ct);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static AppException InvalidCredentials() =>
        AppException.Unauthorized("INVALID_CREDENTIALS", "Неверный email или пароль");

    private static AppException InvalidRefreshToken() =>
        AppException.Unauthorized("INVALID_REFRESH_TOKEN", "Сессия истекла, войдите снова");
}

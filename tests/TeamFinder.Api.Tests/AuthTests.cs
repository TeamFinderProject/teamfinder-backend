using System.Net;
using System.Net.Http.Json;

namespace TeamFinder.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(TestApp app)
{
    [Fact]
    public async Task Register_returns_access_token_and_sets_refresh_cookie()
    {
        var user = await app.RegisterAsync("Иван");

        Assert.False(string.IsNullOrEmpty(user.RefreshCookie));
        var profile = await user.Client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal("Иван", (await Json.ReadAsync(profile)).GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Register_with_taken_email_returns_409()
    {
        var user = await app.RegisterAsync();
        var response = await app.Anonymous().PostAsJsonAsync("/api/auth/register",
            new { email = user.Email.ToUpperInvariant(), password = "Secret123!", displayName = "Двойник" });

        await Json.AssertErrorAsync(response, HttpStatusCode.Conflict, "EMAIL_TAKEN");
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_generic_401()
    {
        var user = await app.RegisterAsync();
        var response = await app.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "wrong-password" });

        await Json.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        var response = await app.Anonymous().GetAsync("/api/profile");
        await Json.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Refresh_rotates_token_and_reuse_of_old_token_revokes_family()
    {
        var user = await app.RegisterAsync();
        var client = app.Anonymous();

        // 1. Обычное обновление: выдан новый refresh token.
        var first = await RefreshAsync(client, user.RefreshCookie!);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = Json.RefreshCookie(first);
        Assert.False(string.IsNullOrEmpty(rotated));
        Assert.NotEqual(user.RefreshCookie, rotated);

        // 2. Старый токен предъявлен повторно — считаем кражей.
        var reuse = await RefreshAsync(client, user.RefreshCookie!);
        await Json.AssertErrorAsync(reuse, HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN");

        // 3. Вся цепочка отозвана: новый токен тоже больше не работает.
        var afterTheft = await RefreshAsync(client, rotated!);
        await Json.AssertErrorAsync(afterTheft, HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var user = await app.RegisterAsync();
        var client = app.Anonymous();

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"tf_refresh={user.RefreshCookie}");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);

        var refresh = await RefreshAsync(client, user.RefreshCookie!);
        await Json.AssertErrorAsync(refresh, HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN");
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"tf_refresh={token}");
        return client.SendAsync(request);
    }
}

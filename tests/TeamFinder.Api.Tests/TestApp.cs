using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TeamFinder.Api.Data;

namespace TeamFinder.Api.Tests;

/// <summary>
/// Поднимает API в памяти поверх настоящего PostgreSQL. Отдельная база teamfinder_tests
/// пересоздаётся один раз на прогон. Строка подключения — переменная TEST_DB_CONNECTION
/// (в CI задаётся в workflow), по умолчанию — локальный PostgreSQL из docker-compose.
/// </summary>
public sealed class TestApp : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=teamfinder_tests;Username=teamfinder;Password=teamfinder_local";

    public WebApplicationFactory<Program> Factory { get; }

    public TestApp()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION") ?? DefaultConnection;
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Своё окружение: appsettings.Development.json (демо-данные, ключ разработчика) в тестах не читается.
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:TeamFinder", connection);
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("Jwt:SigningKey", "test-signing-key-0123456789-0123456789-abcdef");
            builder.UseSetting("Jwt:SecureCookie", "false");
            // В тестах повторное использование старого refresh token сразу считается кражей.
            builder.UseSetting("Jwt:RefreshReuseGraceSeconds", "0");
            // Telegram и демо-данные в тестах выключены.
            builder.UseSetting("Telegram:BotToken", "");
            builder.UseSetting("Demo:SeedOnStartup", "false");
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TeamFinderDbContext>();
        await db.Database.EnsureDeletedAsync();
        // Схема строится по той же модели, что и миграции. Сами миграции проверяет отдельный шаг CI.
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();

    public HttpClient Anonymous() => Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public async Task<TestUser> RegisterAsync(string name = "Пользователь")
    {
        var client = Anonymous();
        var email = $"{Guid.NewGuid():N}@test.local";
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "Secret123!", displayName = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Json.ReadAsync(response);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return new TestUser(body.GetProperty("user").GetProperty("id").GetInt64(), email, client,
            Json.RefreshCookie(response));
    }

    /// <summary>Проект в наборе с ролями. Возвращает id проекта и id ролей в порядке передачи.</summary>
    public static async Task<(long ProjectId, long[] RoleIds)> CreateRecruitingProjectAsync(TestUser owner, params int[] capacities)
    {
        var roles = capacities.Select(c => new
        {
            roleTypeId = 1,
            capacity = c,
            requirements = new object[] { new { skillId = 1, type = "REQUIRED", minimumLevel = "INTERMEDIATE" } },
        }).ToArray();

        var response = await owner.Client.PostAsJsonAsync("/api/projects", new
        {
            title = "Тестовый проект",
            description = "Описание тестового проекта",
            type = "PET_PROJECT",
            externalContactUrl = "https://t.me/test",
            publish = true,
            roles,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Json.ReadAsync(response);
        var roleIds = body.GetProperty("roles").EnumerateArray().Select(r => r.GetProperty("id").GetInt64()).ToArray();
        return (body.GetProperty("id").GetInt64(), roleIds);
    }

    public static async Task<long> ApplyAsync(TestUser candidate, long projectId, long roleId)
    {
        var response = await candidate.Client.PostAsJsonAsync(
            $"/api/projects/{projectId}/roles/{roleId}/applications", new { message = "Хочу в команду" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json.ReadAsync(response)).GetProperty("id").GetInt64();
    }
}

public sealed record TestUser(long Id, string Email, HttpClient Client, string? RefreshCookie);

public static class Json
{
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(string.IsNullOrEmpty(text) ? "null" : text).RootElement.Clone();
    }

    public static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await ReadAsync(response);
        Assert.True(status == response.StatusCode, $"Ожидался {(int)status}, получен {(int)response.StatusCode}: {body}");
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    /// <summary>Значение cookie tf_refresh из Set-Cookie: "tf_refresh=abc; path=/api/auth; ..."</summary>
    public static string? RefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(v => v.Split(';')[0])
                .Where(v => v.StartsWith("tf_refresh=", StringComparison.Ordinal))
                .Select(v => v["tf_refresh=".Length..])
                .FirstOrDefault(v => v.Length > 0)
            : null;
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<TestApp>
{
    public const string Name = "api";
}

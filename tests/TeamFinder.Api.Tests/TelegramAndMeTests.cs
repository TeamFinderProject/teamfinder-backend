using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TeamFinder.Api.Data;
using TeamFinder.Api.Entities;
using TeamFinder.Api.Notifications.Telegram;

namespace TeamFinder.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class TelegramAndMeTests(TestApp app)
{
    [Fact]
    public async Task Me_returns_current_user()
    {
        var user = await app.RegisterAsync("Ольга");
        var me = await Json.ReadAsync(await user.Client.GetAsync("/api/auth/me"));

        Assert.Equal(user.Id, me.GetProperty("id").GetInt64());
        Assert.Equal("Ольга", me.GetProperty("displayName").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Anonymous().GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Telegram_link_is_refused_when_bot_is_not_configured()
    {
        var user = await app.RegisterAsync();

        var status = await Json.ReadAsync(await user.Client.GetAsync("/api/profile/telegram"));
        Assert.False(status.GetProperty("enabled").GetBoolean());
        Assert.False(status.GetProperty("linked").GetBoolean());

        var link = await user.Client.PostAsync("/api/profile/telegram/link", null);
        await Json.AssertErrorAsync(link, HttpStatusCode.Conflict, "TELEGRAM_DISABLED");
    }

    [Fact]
    public async Task Start_command_with_code_links_chat_and_stop_unlinks()
    {
        var user = await app.RegisterAsync("Кирилл");
        const long chatId = 987654321;

        using (var scope = app.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TeamFinderDbContext>();
            db.TelegramLinkCodes.Add(new TelegramLinkCode
            {
                Code = "test-code-" + user.Id, UserId = user.Id, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            });
            await db.SaveChangesAsync();

            var telegram = scope.ServiceProvider.GetRequiredService<TelegramService>();
            var reply = await telegram.HandleMessageAsync(chatId, "/start test-code-" + user.Id, CancellationToken.None);
            Assert.Contains("Кирилл", reply);

            // Код одноразовый.
            var again = await telegram.HandleMessageAsync(chatId, "/start test-code-" + user.Id, CancellationToken.None);
            Assert.Contains("устарела", again);
        }

        var status = await Json.ReadAsync(await user.Client.GetAsync("/api/profile/telegram"));
        Assert.True(status.GetProperty("linked").GetBoolean());

        using (var scope = app.Factory.Services.CreateScope())
        {
            var telegram = scope.ServiceProvider.GetRequiredService<TelegramService>();
            await telegram.HandleMessageAsync(chatId, "/stop", CancellationToken.None);
        }

        status = await Json.ReadAsync(await user.Client.GetAsync("/api/profile/telegram"));
        Assert.False(status.GetProperty("linked").GetBoolean());
    }

    [Fact]
    public async Task Demo_data_seeder_creates_scenario_once()
    {
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TeamFinderDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();

        await DemoDataSeeder.SeedAsync(db, hasher, TimeProvider.System);
        await DemoDataSeeder.SeedAsync(db, hasher, TimeProvider.System); // второй вызов ничего не дублирует

        Assert.Equal(1, db.Users.Count(u => u.Email == "ivan@demo.local"));
        var login = await app.Anonymous().PostAsync("/api/auth/login",
            new StringContent("{\"email\":\"petr@demo.local\",\"password\":\"Demo12345!\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}

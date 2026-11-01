using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TeamFinder.Api.Tests;

/// <summary>Машина состояний заявки и правила вместимости (business-rules.md: RULE-APP-*, RULE-CAP-*).</summary>
[Collection(ApiCollection.Name)]
public sealed class ApplicationTests(TestApp app)
{
    [Fact]
    public async Task Apply_creates_pending_application_and_notifies_owner()
    {
        var owner = await app.RegisterAsync();
        var candidate = await app.RegisterAsync("Пётр");
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 1);

        await TestApp.ApplyAsync(candidate, projectId, roles[0]);

        var mine = await Json.ReadAsync(await candidate.Client.GetAsync("/api/applications/mine"));
        Assert.Equal("PENDING", mine[0].GetProperty("status").GetString());
        Assert.True(mine[0].GetProperty("canWithdraw").GetBoolean());

        var notifications = await Json.ReadAsync(await owner.Client.GetAsync("/api/notifications"));
        Assert.Contains(notifications.EnumerateArray(), n => n.GetProperty("type").GetString() == "APPLICATION_RECEIVED");
    }

    [Fact]
    public async Task Second_application_to_same_role_is_rejected()
    {
        var owner = await app.RegisterAsync();
        var candidate = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 1);
        await TestApp.ApplyAsync(candidate, projectId, roles[0]);

        var again = await candidate.Client.PostAsJsonAsync($"/api/projects/{projectId}/roles/{roles[0]}/applications", new { });
        await Json.AssertErrorAsync(again, HttpStatusCode.Conflict, "DUPLICATE_APPLICATION");
    }

    [Fact]
    public async Task Owner_cannot_apply_to_own_project()
    {
        var owner = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 1);

        var response = await owner.Client.PostAsJsonAsync($"/api/projects/{projectId}/roles/{roles[0]}/applications", new { });
        await Json.AssertErrorAsync(response, HttpStatusCode.Conflict, "OWNER_CANNOT_APPLY");
    }

    [Fact]
    public async Task Role_from_another_project_is_not_found()
    {
        var owner = await app.RegisterAsync();
        var candidate = await app.RegisterAsync();
        var (projectA, _) = await TestApp.CreateRecruitingProjectAsync(owner, 1);
        var (_, rolesB) = await TestApp.CreateRecruitingProjectAsync(owner, 1);

        var response = await candidate.Client.PostAsJsonAsync($"/api/projects/{projectA}/roles/{rolesB[0]}/applications", new { });
        await Json.AssertErrorAsync(response, HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [Fact]
    public async Task Accept_creates_membership_and_closes_other_applications()
    {
        var owner = await app.RegisterAsync();
        var candidate = await app.RegisterAsync();
        var rival = await app.RegisterAsync();
        // Роль 0 — одно место, роль 1 — два места.
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 1, 2);

        var accepted = await TestApp.ApplyAsync(candidate, projectId, roles[0]);
        var candidateOtherRole = await TestApp.ApplyAsync(candidate, projectId, roles[1]);
        var rivalSameRole = await TestApp.ApplyAsync(rival, projectId, roles[0]);

        var response = await owner.Client.PostAsync($"/api/applications/{accepted}/accept", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json.ReadAsync(response);
        Assert.Equal("ACCEPTED", body.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("membershipId").ValueKind);

        var list = await Json.ReadAsync(await owner.Client.GetAsync($"/api/projects/{projectId}/applications"));
        Assert.Equal("ACCEPTED_ANOTHER_ROLE", CloseReason(list, candidateOtherRole));
        Assert.Equal("ROLE_FILLED", CloseReason(list, rivalSameRole));

        var team = await Json.ReadAsync(await candidate.Client.GetAsync($"/api/projects/{projectId}/team"));
        Assert.Equal(candidate.Id, team.GetProperty("members")[0].GetProperty("userId").GetInt64());
        Assert.Equal("https://t.me/test", team.GetProperty("externalContactUrl").GetString());
    }

    [Fact]
    public async Task Repeated_accept_does_not_create_second_membership()
    {
        var owner = await app.RegisterAsync();
        var candidate = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 2);
        var applicationId = await TestApp.ApplyAsync(candidate, projectId, roles[0]);

        Assert.Equal(HttpStatusCode.OK, (await owner.Client.PostAsync($"/api/applications/{applicationId}/accept", null)).StatusCode);
        var second = await owner.Client.PostAsync($"/api/applications/{applicationId}/accept", null);
        await Json.AssertErrorAsync(second, HttpStatusCode.Conflict, "APPLICATION_NOT_PENDING");

        var team = await Json.ReadAsync(await owner.Client.GetAsync($"/api/projects/{projectId}/team"));
        Assert.Equal(1, team.GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task Withdraw_only_while_pending()
    {
        var owner = await app.RegisterAsync();
        var candidate = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 1, 1);
        var pending = await TestApp.ApplyAsync(candidate, projectId, roles[0]);
        var toAccept = await TestApp.ApplyAsync(candidate, projectId, roles[1]);

        var withdraw = await candidate.Client.PostAsync($"/api/applications/{pending}/withdraw", null);
        Assert.Equal("WITHDRAWN", (await Json.ReadAsync(withdraw)).GetProperty("status").GetString());

        await owner.Client.PostAsync($"/api/applications/{toAccept}/accept", null);
        var afterAccept = await candidate.Client.PostAsync($"/api/applications/{toAccept}/withdraw", null);
        await Json.AssertErrorAsync(afterAccept, HttpStatusCode.Conflict, "APPLICATION_NOT_PENDING");
    }

    [Fact]
    public async Task Only_owner_accepts_and_only_author_withdraws()
    {
        var owner = await app.RegisterAsync();
        var candidate = await app.RegisterAsync();
        var stranger = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 1);
        var applicationId = await TestApp.ApplyAsync(candidate, projectId, roles[0]);

        await Json.AssertErrorAsync(await stranger.Client.PostAsync($"/api/applications/{applicationId}/accept", null),
            HttpStatusCode.Forbidden, "FORBIDDEN");
        await Json.AssertErrorAsync(await stranger.Client.PostAsync($"/api/applications/{applicationId}/withdraw", null),
            HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [Fact]
    public async Task Parallel_accepts_for_last_seat_admit_only_one()
    {
        var owner = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 1);
        var candidates = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => app.RegisterAsync()));
        var applications = new List<long>();
        foreach (var c in candidates)
            applications.Add(await TestApp.ApplyAsync(c, projectId, roles[0]));

        var results = await Task.WhenAll(applications.Select(id => owner.Client.PostAsync($"/api/applications/{id}/accept", null)));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        var team = await Json.ReadAsync(await owner.Client.GetAsync($"/api/projects/{projectId}/team"));
        Assert.Equal(1, team.GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task Starting_project_closes_pending_applications()
    {
        var owner = await app.RegisterAsync();
        var candidate = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 1);
        var applicationId = await TestApp.ApplyAsync(candidate, projectId, roles[0]);

        var start = await owner.Client.PostAsync($"/api/projects/{projectId}/start", null);
        Assert.Equal("IN_PROGRESS", (await Json.ReadAsync(start)).GetProperty("state").GetString());

        var list = await Json.ReadAsync(await owner.Client.GetAsync($"/api/projects/{projectId}/applications"));
        Assert.Equal("RECRUITMENT_CLOSED", CloseReason(list, applicationId));

        var lateApply = await candidate.Client.PostAsJsonAsync($"/api/projects/{projectId}/roles/{roles[0]}/applications", new { });
        Assert.Equal(HttpStatusCode.Conflict, lateApply.StatusCode);
    }

    [Fact]
    public async Task Closing_role_closes_its_pending_applications()
    {
        var owner = await app.RegisterAsync();
        var candidate = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 2);
        var applicationId = await TestApp.ApplyAsync(candidate, projectId, roles[0]);

        var close = await owner.Client.PostAsync($"/api/projects/{projectId}/roles/{roles[0]}/close", null);
        Assert.True((await Json.ReadAsync(close)).GetProperty("isClosed").GetBoolean());

        var list = await Json.ReadAsync(await owner.Client.GetAsync($"/api/projects/{projectId}/applications"));
        Assert.Equal("ROLE_CLOSED", CloseReason(list, applicationId));
    }

    private static string? CloseReason(JsonElement list, long applicationId) =>
        list.EnumerateArray().Single(a => a.GetProperty("id").GetInt64() == applicationId)
            .GetProperty("closeReason").GetString();
}

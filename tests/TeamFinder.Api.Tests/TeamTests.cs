using System.Net;
using System.Net.Http.Json;

namespace TeamFinder.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class TeamTests(TestApp app)
{
    [Fact]
    public async Task Owner_removes_member_with_ids_in_url()
    {
        var owner = await app.RegisterAsync();
        var member = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 1);
        var applicationId = await TestApp.ApplyAsync(member, projectId, roles[0]);
        await owner.Client.PostAsync($"/api/applications/{applicationId}/accept", null);

        var delete = await owner.Client.DeleteAsync($"/api/projects/{projectId}/members/{member.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var team = await Json.ReadAsync(await owner.Client.GetAsync($"/api/projects/{projectId}/team"));
        Assert.Equal(0, team.GetProperty("members").GetArrayLength());

        // Исключённый больше не видит команду и контакт (RULE-TEAM-04).
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync($"/api/projects/{projectId}/team")).StatusCode);
        var project = await Json.ReadAsync(await member.Client.GetAsync($"/api/projects/{projectId}"));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, project.GetProperty("externalContactUrl").ValueKind);
    }

    [Fact]
    public async Task Member_leaves_and_seat_is_released()
    {
        var owner = await app.RegisterAsync();
        var member = await app.RegisterAsync();
        var next = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 1);
        var applicationId = await TestApp.ApplyAsync(member, projectId, roles[0]);
        await owner.Client.PostAsync($"/api/applications/{applicationId}/accept", null);

        Assert.Equal(HttpStatusCode.NoContent, (await member.Client.PostAsync($"/api/projects/{projectId}/team/leave", null)).StatusCode);

        // Место освободилось — новый кандидат может подать заявку.
        await TestApp.ApplyAsync(next, projectId, roles[0]);

        var again = await member.Client.PostAsync($"/api/projects/{projectId}/team/leave", null);
        await Json.AssertErrorAsync(again, HttpStatusCode.Conflict, "NOT_A_MEMBER");
    }

    [Fact]
    public async Task Stranger_cannot_remove_members()
    {
        var owner = await app.RegisterAsync();
        var stranger = await app.RegisterAsync();
        var (projectId, _) = await TestApp.CreateRecruitingProjectAsync(owner, 1);

        var response = await stranger.Client.DeleteAsync($"/api/projects/{projectId}/members/{owner.Id}");
        await Json.AssertErrorAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN");
    }
}

using System.Net;
using System.Net.Http.Json;

namespace TeamFinder.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ProjectTests(TestApp app)
{
    [Fact]
    public async Task Create_project_with_roles_and_requirements_in_one_request()
    {
        var owner = await app.RegisterAsync("Владелец");

        var response = await owner.Client.PostAsJsonAsync("/api/projects", new
        {
            title = "TeamFinder",
            description = "Сервис поиска участников и команд",
            type = "STUDY_PROJECT",
            externalContactUrl = "https://t.me/teamfinder_dev",
            roles = new object[]
            {
                new
                {
                    roleTypeId = 1, description = "API", capacity = 2,
                    requirements = new object[]
                    {
                        new { skillId = 1, type = "REQUIRED", minimumLevel = "INTERMEDIATE" },
                        new { skillId = 9, type = "NICE_TO_HAVE" },
                    },
                },
                new { roleTypeId = 4, title = "UI/UX дизайнер", capacity = 1 },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var project = await Json.ReadAsync(response);
        Assert.Equal("DRAFT", project.GetProperty("state").GetString());
        Assert.Equal("STUDY_PROJECT", project.GetProperty("type").GetString());
        Assert.Equal("OWNER", project.GetProperty("myRelation").GetString());
        Assert.Equal("https://t.me/teamfinder_dev", project.GetProperty("externalContactUrl").GetString());

        var roles = project.GetProperty("roles").EnumerateArray().ToList();
        Assert.Equal(2, roles.Count);
        Assert.Equal("Backend Developer", roles[0].GetProperty("title").GetString()); // взято из справочника
        Assert.Equal(2, roles[0].GetProperty("requirements").GetArrayLength());
        Assert.Equal("UI/UX дизайнер", roles[1].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Invalid_role_is_reported_with_field_path_and_nothing_is_created()
    {
        var owner = await app.RegisterAsync();
        var response = await owner.Client.PostAsJsonAsync("/api/projects", new
        {
            title = "Проект с ошибкой",
            description = "Вторая роль некорректна",
            type = "HACKATHON",
            roles = new object[]
            {
                new { roleTypeId = 1, capacity = 1 },
                new { roleTypeId = 2, capacity = 0 },
            },
        });

        await Json.AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        var body = await Json.ReadAsync(response);
        Assert.True(body.GetProperty("errors").TryGetProperty("roles[1].capacity", out _));

        var mine = await Json.ReadAsync(await owner.Client.GetAsync("/api/projects/mine"));
        Assert.Equal(0, mine.GetArrayLength());
    }

    [Fact]
    public async Task Same_skill_twice_in_role_is_rejected()
    {
        var owner = await app.RegisterAsync();
        var response = await owner.Client.PostAsJsonAsync("/api/projects", new
        {
            title = "Дубль навыка",
            description = "Навык и обязательный, и желательный",
            type = "PET_PROJECT",
            roles = new object[]
            {
                new
                {
                    roleTypeId = 1, capacity = 1,
                    requirements = new object[]
                    {
                        new { skillId = 3, type = "REQUIRED" },
                        new { skillId = 3, type = "NICE_TO_HAVE" },
                    },
                },
            },
        });

        await Json.AssertErrorAsync(response, HttpStatusCode.BadRequest, "DUPLICATE_SKILL");
    }

    [Fact]
    public async Task Publish_without_roles_is_rejected()
    {
        var owner = await app.RegisterAsync();
        var response = await owner.Client.PostAsJsonAsync("/api/projects", new
        {
            title = "Пустой", description = "Проект без ролей", type = "PET_PROJECT", publish = true,
        });

        await Json.AssertErrorAsync(response, HttpStatusCode.Conflict, "PROJECT_HAS_NO_ROLES");
    }

    [Fact]
    public async Task Draft_is_hidden_from_other_users_and_catalog()
    {
        var owner = await app.RegisterAsync();
        var stranger = await app.RegisterAsync();
        var created = await owner.Client.PostAsJsonAsync("/api/projects", new
        {
            title = "Секретный черновик", description = "Ещё не опубликован", type = "PET_PROJECT",
        });
        var id = (await Json.ReadAsync(created)).GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync($"/api/projects/{id}")).StatusCode);
        var catalog = await Json.ReadAsync(await stranger.Client.GetAsync("/api/projects?q=Секретный черновик"));
        Assert.Equal(0, catalog.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Only_owner_can_change_project()
    {
        var owner = await app.RegisterAsync();
        var stranger = await app.RegisterAsync();
        var (projectId, _) = await TestApp.CreateRecruitingProjectAsync(owner, 1);

        var response = await stranger.Client.PatchAsJsonAsync($"/api/projects/{projectId}", new { title = "Захват" });
        await Json.AssertErrorAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [Fact]
    public async Task Role_requirements_are_locked_after_first_application()
    {
        var owner = await app.RegisterAsync();
        var candidate = await app.RegisterAsync();
        var (projectId, roles) = await TestApp.CreateRecruitingProjectAsync(owner, 2);
        await TestApp.ApplyAsync(candidate, projectId, roles[0]);

        var changeSkills = await owner.Client.PatchAsJsonAsync($"/api/projects/{projectId}/roles/{roles[0]}",
            new { requirements = new object[] { new { skillId = 2, type = "REQUIRED" } } });
        await Json.AssertErrorAsync(changeSkills, HttpStatusCode.Conflict, "ROLE_LOCKED");

        var changeDescription = await owner.Client.PatchAsJsonAsync($"/api/projects/{projectId}/roles/{roles[0]}",
            new { description = "Описание можно менять всегда" });
        Assert.Equal(HttpStatusCode.OK, changeDescription.StatusCode);
    }

    [Fact]
    public async Task Catalog_filter_by_type_uses_contract_enum_values()
    {
        var owner = await app.RegisterAsync();
        await TestApp.CreateRecruitingProjectAsync(owner, 1);

        var ok = await owner.Client.GetAsync("/api/projects?type=PET_PROJECT&openOnly=true");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.True((await Json.ReadAsync(ok)).GetProperty("total").GetInt32() >= 1);

        var bad = await owner.Client.GetAsync("/api/projects?type=UNKNOWN");
        await Json.AssertErrorAsync(bad, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
}

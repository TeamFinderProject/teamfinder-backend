using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<TeamFinderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TeamFinder")
        ?? throw new InvalidOperationException(
            "Connection string 'TeamFinder' is missing. Set ConnectionStrings__TeamFinder.")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "TeamFinder API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/database", async (TeamFinderDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { database = "available" })
        : Results.Problem("Database is unavailable.");
});

app.MapControllers();
app.Run();

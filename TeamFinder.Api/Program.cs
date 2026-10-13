using Microsoft.EntityFrameworkCore;
using TeamFinder.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TeamFinder")
    ?? throw new InvalidOperationException(
        "Connection string 'TeamFinder' is missing. Set ConnectionStrings__TeamFinder.");

builder.Services.AddDbContext<TeamFinderDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/database", async (TeamFinderDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { database = "available" })
        : Results.Problem("Database is unavailable.");
});

app.Run();

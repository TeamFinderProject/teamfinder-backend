using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TeamFinder.Api.Applications;
using TeamFinder.Api.Auth;
using TeamFinder.Api.Common;
using TeamFinder.Api.Data;
using TeamFinder.Api.Entities;
using TeamFinder.Api.Notifications;
using TeamFinder.Api.Notifications.Telegram;
using TeamFinder.Api.Profiles;
using TeamFinder.Api.Projects;
using TeamFinder.Api.Teams;

var builder = WebApplication.CreateBuilder(args);

// ---------- HTTP API ----------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
        // Перечисления в JSON: PET_PROJECT, IN_PROGRESS, NICE_TO_HAVE (раздел 2.1 контракта).
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = AppExceptionHandler.InvalidModelState);

// CORS нужен, когда frontend открыт с другого адреса, чем API (например, Vite на :5173 без прокси
// обращается к http://localhost:5017). Без него браузер получает ответ 200, но не отдаёт его в JS —
// именно эту ошибку видели на checkpoint 2. Через прокси Vite или nginx запросы идут с того же адреса.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // refresh token ходит в cookie, поэтому нужны credentials и явный список адресов

builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecurityTransformer>());

// ---------- База данных ----------
builder.Services.AddDbContext<TeamFinderDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("TeamFinder")
        ?? throw new InvalidOperationException(
            "Connection string 'TeamFinder' is missing. Set ConnectionStrings__TeamFinder.")));

// ---------- Аутентификация: JWT access token + refresh token (docs/adr-001-authentication.md) ----------
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey must be at least 32 characters. Set Jwt__SigningKey.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        // Claim "sub" остаётся "sub", без переименования в длинные URI.
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TokenService.CreateSigningKey(jwt.Value.SigningKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
        };
        // 401 и 403 в том же формате, что и остальные ошибки API.
        bearer.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("UNAUTHORIZED", "Требуется вход в систему"));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("FORBIDDEN", "Недостаточно прав"));
            },
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddLoginRateLimit(builder.Configuration.GetValue("Jwt:LoginAttemptsPerMinute", 10));

// За nginx (Docker) все запросы приходят с адреса контейнера frontend. Без этого лимит попыток входа
// был бы общим на всех пользователей. Берём реальный IP из X-Forwarded-For, но доверяем заголовку
// только от прокси из частных сетей (сеть Docker), чтобы его нельзя было подделать снаружи.
var trustProxy = builder.Configuration.GetValue<bool>("ReverseProxy:TrustPrivateNetworks");
if (trustProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // KnownIPNetworks появилось в .NET 10 (старое KnownNetworks помечено устаревшим).
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var network in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" })
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    });
}

// ---------- Telegram-уведомления (выключены, если Telegram:BotToken пустой) ----------
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.AddHttpClient<TelegramClient>();
builder.Services.AddScoped<TelegramService>();
builder.Services.AddHostedService<TelegramWorker>();

// ---------- Сервисы модулей ----------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<MatchingService>();
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<NotificationService>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<TeamFinderDbContext>().Database.MigrateAsync();
}

// Демо-данные для показа (docs/demo-guide.md): пользователи ivan/petr/anna/maria@demo.local, пароль Demo12345!
if (app.Configuration.GetValue<bool>("Demo:SeedOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await DemoDataSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<TeamFinderDbContext>(),
        scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
        TimeProvider.System);
}

if (trustProxy) app.UseForwardedHeaders();
app.UseExceptionHandler();

// Swagger UI показывает два документа: контракт (docs/openapi.yaml) и описание, сгенерированное из кода.
// Так на checkpoint видно, совпадает ли реализация с контрактом.
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".yaml"] = "application/yaml";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/contract/openapi.yaml", "Контракт (docs/openapi.yaml)");
    options.SwaggerEndpoint("/openapi/v1.json", "Реализация (из кода)");
    options.DocumentTitle = "TeamFinder API";
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/health/database", async (TeamFinderDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { database = "available" })
        : Results.Problem("Database is unavailable.")).AllowAnonymous();

app.MapControllers();
app.Run();

/// <summary>Нужен интеграционным тестам (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program { }

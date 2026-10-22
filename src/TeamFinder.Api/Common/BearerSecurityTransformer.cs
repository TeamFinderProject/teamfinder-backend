using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TeamFinder.Api.Common;

/// <summary>
/// Добавляет в OpenAPI-документ «Реализация» схему авторизации Bearer (JWT).
/// Благодаря этому в Swagger UI появляется кнопка Authorize: вставляете accessToken из ответа
/// /api/auth/login, и защищённые запросы можно вызывать прямо из Swagger.
/// </summary>
public sealed class BearerSecurityTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "accessToken из ответа POST /api/auth/login (живёт 15 минут)",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });
        return Task.CompletedTask;
    }
}

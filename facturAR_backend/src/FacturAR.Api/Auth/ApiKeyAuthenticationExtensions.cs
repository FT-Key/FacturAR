using FacturAR.Contracts.Comunes;
using Microsoft.AspNetCore.Authentication;

namespace FacturAR.Api.Auth;

public static class ApiKeyAuthenticationExtensions
{
    public const string PolicyName = "ApiKeyNegocio";

    public static IServiceCollection AddFacturarApiKeyAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ApiKeyAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = ApiKeyAuthenticationHandler.SchemeName;
                options.DefaultForbidScheme = ApiKeyAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                _ => { });

        services.AddAuthorization(options => options.AddPolicy(PolicyName, policy => policy
            .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(ApiKeyAuthenticationHandler.ClaimContribuyenteId)));

        return services;
    }

    public static RouteHandlerBuilder RequireApiKey(this RouteHandlerBuilder builder)
        => builder
            .RequireAuthorization(PolicyName)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized);

    public static RouteGroupBuilder RequireApiKey(this RouteGroupBuilder builder)
        => builder.RequireAuthorization(PolicyName);
}

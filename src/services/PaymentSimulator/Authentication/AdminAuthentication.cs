using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PaymentSimulator.Authentication;

public static class AdminAuthentication
{
    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services,
        IConfiguration configuration, bool isDevelopment)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, InternalApiKeyAuthenticationHandler>(
                InternalApiKeyAuthenticationHandler.SchemeName, _ => { })
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Authentication:Authority"]
                    ?? "http://localhost:8180/realms/halsingelive";
                if (configuration["Authentication:MetadataAddress"] is { Length: > 0 } metadataAddress)
                    options.MetadataAddress = metadataAddress;
                options.Audience = "eventcatalog";
                options.RequireHttpsMetadata = !isDevelopment;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Authority.TrimEnd('/'),
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles"
                };
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
            {
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole("admin");
            });
            options.AddPolicy(InternalApiKeyAuthenticationHandler.PolicyName, policy =>
            {
                policy.AddAuthenticationSchemes(InternalApiKeyAuthenticationHandler.SchemeName);
                policy.RequireAuthenticatedUser();
            });
        });
        return services;
    }
}

public sealed class InternalApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "InternalApiKey";
    public const string PolicyName = "InternalPayments";
    public const string HeaderName = "X-Internal-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var providedKeys))
            return Task.FromResult(AuthenticateResult.NoResult());

        var configuredKey = configuration["PaymentSimulator:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey) || providedKeys.Count != 1
            || string.IsNullOrWhiteSpace(providedKeys[0]))
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal API key."));

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKeys[0]!));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        if (!CryptographicOperations.FixedTimeEquals(providedHash, configuredHash))
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal API key."));

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "booking")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

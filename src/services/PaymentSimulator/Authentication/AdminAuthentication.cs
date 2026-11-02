using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace PaymentSimulator.Authentication;

public static class AdminAuthentication
{
    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services,
        IConfiguration configuration, bool isDevelopment)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
        services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole("admin");
        }));
        return services;
    }
}

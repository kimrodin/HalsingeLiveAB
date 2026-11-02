using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PaymentSimulator.Authentication;
using PaymentSimulator.Features.Payments.Simulation;

namespace PaymentSimulator.UnitTests;

public sealed class AdminAuthorizationTests
{
    [Theory]
    [InlineData(false, "admin", false)]
    [InlineData(true, null, false)]
    [InlineData(true, "customer", false)]
    [InlineData(true, "admin", true)]
    public async Task Settings_policy_requires_authenticated_admin(bool authenticated, string? role, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAdminAuthentication(new ConfigurationBuilder().Build(), isDevelopment: true);
        using var provider = services.BuildServiceProvider();
        var claims = role is null ? Array.Empty<Claim>() : new[] { new Claim("roles", role) };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims,
            authenticated ? "Bearer" : null, "preferred_username", "roles"));
        var attribute = Assert.Single(typeof(SimulationSettingsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>());
        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(user, resource: null, attribute.Policy!);
        Assert.Equal(allowed, result.Succeeded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Jwt_configuration_matches_other_services(bool isDevelopment)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = "https://login.example/realms/halsingelive/",
            ["Authentication:MetadataAddress"] = "https://internal.example/discovery"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAdminAuthentication(configuration, isDevelopment);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.Equal("https://login.example/realms/halsingelive/", options.Authority);
        Assert.Equal("https://internal.example/discovery", options.MetadataAddress);
        Assert.Equal("https://login.example/realms/halsingelive", options.TokenValidationParameters.ValidIssuer);
        Assert.Equal("eventcatalog", options.Audience);
        Assert.Equal("roles", options.TokenValidationParameters.RoleClaimType);
        Assert.Equal("preferred_username", options.TokenValidationParameters.NameClaimType);
        Assert.False(options.MapInboundClaims);
        Assert.Equal(!isDevelopment, options.RequireHttpsMetadata);
    }
}

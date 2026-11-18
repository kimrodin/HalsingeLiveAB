using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PaymentSimulator.Authentication;
using PaymentSimulator.Features.Payments;

namespace PaymentSimulator.UnitTests;

public sealed class InternalPaymentAuthorizationTests
{
    private const string ApiKey = "payment-authorization-test-key";
    private const string Issuer = "https://payment-tests.example/realms/halsingelive";
    private static readonly SymmetricSecurityKey SigningKey = new(RandomNumberGenerator.GetBytes(32));

    public static TheoryData<string, string, string, HttpStatusCode> PaymentRequests
    {
        get
        {
            var data = new TheoryData<string, string, string, HttpStatusCode>();
            var endpoints = new[]
            {
                ("POST", "/api/payments"),
                ("GET", $"/api/payments/by-booking/{Guid.Empty}"),
                ("GET", $"/api/payments/{Guid.Empty}"),
                ("POST", $"/api/payments/{Guid.Empty}/refund"),
                ("POST", "/api/payments/cancel")
            };
            foreach (var (method, path) in endpoints)
                foreach (var scenario in new[] { "anonymous", "wrong-key", "duplicate-key", "query-key", "admin-token", "unconfigured", "valid-key" })
                    data.Add(method, path, scenario,
                        scenario == "valid-key" ? HttpStatusCode.BadRequest : HttpStatusCode.Unauthorized);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(PaymentRequests))]
    public async Task Payment_operations_require_the_internal_key(string method, string path, string scenario,
        HttpStatusCode expected)
    {
        await using var app = CreateHost(scenario == "unconfigured" ? "" : ApiKey);
        await app.StartAsync();
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(new HttpMethod(method),
            scenario == "query-key" ? $"{path}?apiKey={ApiKey}" : path);
        if (method == "POST")
            // Invalid input makes an authenticated request return 400 without touching a database.
            request.Content = JsonContent.Create(new { bookingId = "invalid-id", amount = 100m, currency = "SEK" });
        if (scenario is "valid-key" or "unconfigured")
            request.Headers.Add("X-Internal-Api-Key", ApiKey);
        else if (scenario == "wrong-key")
            request.Headers.Add("X-Internal-Api-Key", "incorrect-key");
        else if (scenario == "duplicate-key")
            request.Headers.Add("X-Internal-Api-Key", new[] { ApiKey, ApiKey });
        else if (scenario == "admin-token")
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        using var response = await client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/admin/payment-simulation")]
    [InlineData("PUT", "/api/admin/payment-simulation")]
    [InlineData("GET", "/api/payments/sales")]
    [InlineData("POST", "/api/payments/seed")]
    [InlineData("DELETE", "/api/payments/seed")]
    public async Task Internal_key_does_not_grant_admin_access(string method, string path)
    {
        await using var app = CreateHost(ApiKey);
        await app.StartAsync();
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("X-Internal-Api-Key", ApiKey);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WebApplication CreateHost(string configuredKey)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Configuration["Authentication:Authority"] = Issuer;
        builder.Configuration["PaymentSimulator:InternalApiKey"] = configuredKey;
        builder.Services.AddAdminAuthentication(builder.Configuration, isDevelopment: true);
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new()
            {
                Issuer = Issuer,
                SigningKeys = { SigningKey }
            }));
        builder.Services.AddControllers().AddApplicationPart(typeof(PaymentsController).Assembly)
            .AddControllersAsServices();
        builder.Services.AddScoped(_ => new PaymentsController(null!, null!, null!, null!));
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app) => new()
    {
        BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single())
    };

    private static string CreateAdminToken() => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer: Issuer, audience: "eventcatalog", claims: [new Claim("roles", "admin")],
        notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(5),
        signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)));
}

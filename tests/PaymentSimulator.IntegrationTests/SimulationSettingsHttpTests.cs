using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Payments.Simulation;
using PaymentSimulator.Messaging.Requests;
using PaymentSimulator.Messaging.Results;
using Microsoft.Extensions.Hosting;

namespace PaymentSimulator.IntegrationTests;

// Hosts the real entry point and JWT middleware. Only identity-provider discovery
// is replaced, so signed test tokens work without a running Keycloak instance.
public sealed class SimulationSettingsHttpTests : IAsyncLifetime
{
    private const string Path = "/api/admin/payment-simulation";
    private const string Issuer = "https://payment-tests.example/realms/halsingelive";
    private static readonly SymmetricSecurityKey SigningKey = new(RandomNumberGenerator.GetBytes(32));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly PaymentSimulatorFactory factory = new();
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        await factory.DisposeAsync();
        await using var db = new PaymentDbContext(new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(factory.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Admin_can_read_update_and_reload_settings()
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin"));

        using var initial = await client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        Assert.Equal(new(SimulationOutcome.Succeeded, 500, 5000),
            await initial.Content.ReadFromJsonAsync<PaymentSimulationSettings>(JsonOptions));

        var settings = new PaymentSimulationSettings(SimulationOutcome.Random, 250, 4000);
        using var updated = await client.PutAsJsonAsync(Path, settings, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(settings, await updated.Content.ReadFromJsonAsync<PaymentSimulationSettings>(JsonOptions));

        using var reloaded = await client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, reloaded.StatusCode);
        Assert.Equal(settings, await reloaded.Content.ReadFromJsonAsync<PaymentSimulationSettings>(JsonOptions));
    }

    [Theory]
    [InlineData("GET", null, HttpStatusCode.Unauthorized)]
    [InlineData("PUT", null, HttpStatusCode.Unauthorized)]
    [InlineData("GET", "customer", HttpStatusCode.Forbidden)]
    [InlineData("PUT", "customer", HttpStatusCode.Forbidden)]
    public async Task Settings_require_an_authenticated_admin(string method, string? role, HttpStatusCode expected)
    {
        using var request = CreateRequest(method);
        if (role is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role));

        using var response = await client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Admin_token_with_invalid_signature_returns_unauthorized(string method)
    {
        var untrustedKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        using var request = CreateRequest(method);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", untrustedKey));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), Path);
        if (method == "PUT")
            request.Content = JsonContent.Create(
                new PaymentSimulationSettings(SimulationOutcome.Failed, 100, 5000), options: JsonOptions);
        return request;
    }

    private static string CreateToken(string role, SecurityKey? signingKey = null)
    {
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: "eventcatalog",
            claims: [new Claim("roles", role)],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class PaymentSimulatorFactory : WebApplicationFactory<Program>
    {
        public string ConnectionString { get; } = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("PAYMENT_TEST_DB")
                ?? "Host=localhost;Port=5434;Database=postgres;Username=postgres;Password=postgres")
        {
            Database = "payment_http_test_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:PaymentSimulator", ConnectionString);
            builder.UseSetting("Authentication:Authority", Issuer);
            builder.UseSetting("PaymentSimulation:Outcome", "Succeeded");
            builder.UseSetting("PaymentSimulation:DelayMs", "500");
            builder.UseSetting("PaymentSimulation:TimeoutMs", "5000");
            builder.UseSetting("Logging:LogLevel:Default", "Warning");
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            builder.ConfigureServices(services =>
            {
                // HTTP tests must not consume requests from a developer's or CI's broker.
                var messaging = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && (descriptor.ImplementationType == typeof(PaymentRequestedConsumer)
                        || descriptor.ImplementationType == typeof(PaymentResultOutboxWorker))).ToArray();
                foreach (var service in messaging) services.Remove(service);
            });
            builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new()
                    {
                        Issuer = Issuer,
                        SigningKeys = { SigningKey }
                    });
                }));
        }
    }
}

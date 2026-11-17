using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Booking.Data;
using Booking.Features.EventInventory;
using Booking.Features.EventInventory.CreateEventInventory;
using Booking.Features.EventInventory.EventCatalog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Booking.UnitTests;

public sealed class InventoryAuthorizationTests
{
    private const string Issuer = "https://identity.example/realms/test";
    private static readonly SymmetricSecurityKey Key = new(new byte[64].Select((_, i) => (byte)(i + 1)).ToArray());

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-audience", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-issuer", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-signature", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("non-admin", HttpStatusCode.Forbidden)]
    [InlineData("missing-role", HttpStatusCode.Forbidden)]
    [InlineData("valid", HttpStatusCode.Created)]
    [InlineData("other-admin", HttpStatusCode.Created)]
    public async Task CatalogCreation_RequiresAdminTokenOverHttp(string scenario, HttpStatusCode expected)
    {
        await using var app = CreateHost();
        await app.StartAsync();
        using var client = CreateClient(app);
        if (scenario != "anonymous")
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(scenario));

        using var response = await client.PostAsJsonAsync(
            $"/CreateEventInventoryFromCatalog/from-catalog/{Guid.NewGuid()}",
            new { Price = 0m, TotalQuantity = 999999 });

        Assert.Equal(expected, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<BookingDbContext>().EventInventory.ToArrayAsync();
        if (expected == HttpStatusCode.Created)
        {
            // Only catalog values are persisted, even when the caller includes fabricated values.
            var row = Assert.Single(rows);
            Assert.Equal(495m, row.Price);
            Assert.Equal(17, row.TotalQuantity);
            Assert.Equal(17, row.AvailableQuantity);
        }
        else
        {
            Assert.Empty(rows);
        }
    }

    [Fact]
    public async Task InventoryReads_RemainAnonymousAndRawCreationIsNotRouted()
    {
        await using var app = CreateHost();
        await app.StartAsync();
        using var client = CreateClient(app);

        using var read = await client.GetAsync($"/GetEventInventory/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.BadRequest, read.StatusCode);
        using var removed = await client.PostAsync($"/CreateEventInventory/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
    }

    private static WebApplication CreateHost()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Configuration["Authentication:Authority"] = Issuer;
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Audience = "eventcatalog";
            options.MapInboundClaims = false;
            options.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
            options.Configuration.SigningKeys.Add(Key);
            options.TokenValidationParameters.ValidIssuer = Issuer;
            options.TokenValidationParameters.RoleClaimType = "roles";
        });
        builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy =>
            policy.RequireAuthenticatedUser().RequireRole("admin")));
        builder.Services.AddControllers().AddApplicationPart(typeof(EventInventoryController).Assembly)
            .AddControllersAsServices();
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<BookingDbContext>(options => options.UseInMemoryDatabase(databaseName)
            // This test checks auth and persistence; PostgreSQL transaction semantics are tested separately.
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddSingleton<IEventCatalogClient, Catalog>();
        builder.Services.AddScoped<CreateEventInventoryFromCatalogHandler>();
        builder.Services.AddScoped(services => new EventInventoryController(
            services.GetRequiredService<CreateEventInventoryFromCatalogHandler>(), null!, null!, builder.Environment));
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

    private static string CreateToken(string scenario)
    {
        var key = scenario == "wrong-signature" ? new SymmetricSecurityKey(new byte[64]) : Key;
        var claims = new List<Claim>
        {
            new("azp", scenario == "other-admin" ? "another-admin-app" : "admin-web")
        };
        if (scenario != "missing-role")
            claims.Add(new Claim("roles", scenario == "non-admin" ? "customer" : "admin"));
        var token = new JwtSecurityToken(
            issuer: scenario == "wrong-issuer" ? "https://other.example" : Issuer,
            audience: scenario == "wrong-audience" ? "booking-inventory" : "eventcatalog",
            claims: claims,
            notBefore: DateTime.UtcNow.AddHours(-1),
            expires: scenario == "expired" ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class Catalog : IEventCatalogClient
    {
        public Task<CatalogEvent> GetEventAsync(Guid eventId, CancellationToken ct) =>
            Task.FromResult(new CatalogEvent([new CatalogEventSectionPrice(Guid.NewGuid(), 495m, 17)]));
    }
}

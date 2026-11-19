using System.Net;
using System.Net.Http.Json;
using HalsingeLive.ClientAddressing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClientAddressing.Tests;

public sealed class GatewayChainTests
{
    [Theory]
    [InlineData("198.51.100.7", "198.51.100.7")]
    [InlineData("::ffff:198.51.100.7", "198.51.100.7")]
    [InlineData("2001:0db8::0007", "2001:db8::7")]
    public async Task TrustedFrontendFlowsThroughYarpToBookingPipeline(string supplied, string expected)
    {
        await using var backend = await StartAsync(trustLoopback: true);
        await using var gateway = await StartAsync(trustLoopback: true, destination: backend.Address);
        Assert.Equal(expected, await ProbeAsync(gateway, supplied));
    }

    [Fact]
    public async Task DirectGatewayCallerCannotChooseItsIdentity()
    {
        await using var backend = await StartAsync(trustLoopback: true);
        await using var gateway = await StartAsync(trustLoopback: false, destination: backend.Address);
        Assert.Equal("127.0.0.1", await ProbeAsync(gateway, "203.0.113.99"));
    }

    [Fact]
    public async Task BackendDoesNotAcceptHeadersFromAnUntrustedGateway()
    {
        await using var backend = await StartAsync(trustLoopback: false);
        await using var gateway = await StartAsync(trustLoopback: true, destination: backend.Address);
        Assert.Equal("127.0.0.1", await ProbeAsync(gateway, "203.0.113.99"));
    }

    [Fact]
    public async Task AutomaticFrameworkForwardingCannotEnableAnEmptyAllowlist()
    {
        await using var backend = await StartAsync(trustLoopback: true);
        await using var gateway = await StartAsync(trustLoopback: false, destination: backend.Address, automaticForwarding: true);
        Assert.Equal("127.0.0.1", await ProbeAsync(gateway, "203.0.113.99"));
    }

    [Fact]
    public async Task AutomaticFrameworkForwardingCannotProcessTwoHops()
    {
        await using var backend = await StartAsync(trustLoopback: true);
        await using var gateway = await StartAsync(trustLoopback: true, destination: backend.Address, automaticForwarding: true);
        Assert.Equal("127.0.0.1", await ProbeAsync(gateway, "203.0.113.99, 127.0.0.1", "https, https"));
    }

    private static async Task<string> ProbeAsync(Host gateway, string supplied, string proto = "https")
    {
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { BaseAddress = new Uri(gateway.Address) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/booking-api/probe");
        request.Headers.Add("X-Forwarded-For", supplied);
        request.Headers.Add("X-Forwarded-Proto", proto);
        request.Headers.Add("X-Real-IP", "203.0.113.98");
        request.Headers.Add("Forwarded", "for=203.0.113.97");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Probe>();
        Assert.NotNull(result);
        Assert.False(result.HasForwardingHeaders);
        return result.Address;
    }

    private static async Task<Host> StartAsync(bool trustLoopback, string? destination = null, bool automaticForwarding = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        if (automaticForwarding)
            builder.WebHost.UseSetting("FORWARDEDHEADERS_ENABLED", "true");
        if (destination is not null)
        {
            // Exercise the actual route configuration shipped by ApiGateway.
            builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "gatewaysettings.json"));
            builder.Configuration["ReverseProxy:Clusters:booking:Destinations:primary:Address"] = destination;
            builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
        }
        if (trustLoopback)
            builder.Configuration["ClientAddressing:TrustedProxies:0"] = "127.0.0.1";
        builder.Services.AddClientAddressing(builder.Configuration);
        var app = builder.Build();
        app.UseClientAddressing();
        if (destination is not null)
            app.MapReverseProxy();
        else
            app.MapGet("/probe", (Microsoft.AspNetCore.Http.HttpContext context) => new Probe(
                context.Connection.RemoteIpAddress!.ToString(),
                context.Request.Headers.Keys.Any(key => key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("X-Real-IP", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Forwarded", StringComparison.OrdinalIgnoreCase))));
        await app.StartAsync();
        return new Host(app, app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single() + "/");
    }

    private sealed record Probe(string Address, bool HasForwardingHeaders);
    private sealed record Host(WebApplication App, string Address) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await App.DisposeAsync();
    }
}

using System.Net;
using HalsingeLive.ClientAddressing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClientAddressing.Tests;

public sealed class ClientAddressingTests
{
    [Theory]
    [InlineData("192.0.2.1", "192.0.2.1")]
    [InlineData("::ffff:192.0.2.1", "192.0.2.1")]
    [InlineData("2001:0db8:0000:0000:0000:0000:0000:0001", "2001:db8::1")]
    [InlineData("fe80::1%12", "fe80::1")]
    public void NormalizationProducesOneIdentity(string input, string expected) =>
        Assert.Equal(expected, ClientAddressingExtensions.NormalizeClientAddress(IPAddress.Parse(input))!.ToString());

    [Fact]
    public void MissingAddressRemainsMissing() =>
        Assert.Null(ClientAddressingExtensions.NormalizeClientAddress(null));

    [Theory]
    [InlineData("10.0.0.2", "198.51.100.7", "198.51.100.7")]
    [InlineData("::ffff:10.0.0.2", "::ffff:198.51.100.7", "198.51.100.7")]
    [InlineData("10.0.0.2", "2001:0db8::0001", "2001:db8::1")]
    public async Task KnownProxyCanSupplyAnAddress(string peer, string forwarded, string expected)
    {
        var context = await RunAsync(peer, ["10.0.0.2"], forwarded);
        Assert.Equal(expected, context.Connection.RemoteIpAddress!.ToString());
        Assert.Equal("https", context.Request.Scheme);
        Assert.Empty(context.Request.Headers);
    }

    [Fact]
    public async Task KnownIpv6ProxyCanSupplyAnAddress()
    {
        var context = await RunAsync("2001:db8::2", ["2001:db8::2"], "2001:db8::7");
        Assert.Equal("2001:db8::7", context.Connection.RemoteIpAddress!.ToString());
    }

    [Theory]
    [InlineData("198.51.100.8", "198.51.100.8")]
    [InlineData("::ffff:198.51.100.8", "198.51.100.8")]
    [InlineData("2001:db8::8", "2001:db8::8")]
    public async Task UnknownPeerCannotSpoofAnyForwardingHeader(string peer, string expected)
    {
        var context = await RunAsync(peer, ["10.0.0.2"], "203.0.113.99");
        Assert.Equal(expected, context.Connection.RemoteIpAddress!.ToString());
        Assert.Equal("http", context.Request.Scheme);
        Assert.Empty(context.Request.Headers);
    }

    [Fact]
    public async Task EmptyAllowlistDoesNotTrustEveryone()
    {
        var context = await RunAsync("127.0.0.1", [], "203.0.113.99");
        Assert.Equal("127.0.0.1", context.Connection.RemoteIpAddress!.ToString());
    }

    [Fact]
    public async Task AHeaderChainCannotExtendTrustBeyondOneHop()
    {
        var context = await RunAsync("10.0.0.2", ["10.0.0.2", "10.0.0.3"],
            "203.0.113.99, 10.0.0.3", "https, https");
        Assert.Equal("10.0.0.3", context.Connection.RemoteIpAddress!.ToString());
        Assert.Empty(context.Request.Headers);
    }

    [Theory]
    [InlineData("not-an-address", "https")]
    [InlineData("203.0.113.99, 198.51.100.7", "https")]
    [InlineData("203.0.113.99", "")]
    public async Task InvalidOrAsymmetricHeadersDoNotReplaceThePeer(string forwarded, string proto)
    {
        var context = await RunAsync("10.0.0.2", ["10.0.0.2"], forwarded, proto);
        Assert.Equal("10.0.0.2", context.Connection.RemoteIpAddress!.ToString());
        Assert.Empty(context.Request.Headers);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("gateway")]
    [InlineData("")]
    public void InvalidProxyConfigurationIsRejected(string proxy) =>
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddClientAddressing(Configuration([proxy])));

    private static async Task<DefaultHttpContext> RunAsync(
        string peer, string[] trusted, string forwarded, string proto = "https")
    {
        using var services = new ServiceCollection().AddLogging().AddClientAddressing(Configuration(trusted))
            .BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseClientAddressing();
        app.Run(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = forwarded;
        if (proto.Length > 0)
            context.Request.Headers["X-Forwarded-Proto"] = proto;
        context.Request.Headers["X-Real-IP"] = "203.0.113.99";
        context.Request.Headers["Forwarded"] = "for=203.0.113.99";
        context.Request.Headers["X-Original-For"] = "203.0.113.99";
        await app.Build()(context);
        return context;
    }

    private static IConfiguration Configuration(string[] trusted) => new ConfigurationBuilder()
        .AddInMemoryCollection(trusted.Select((address, index) =>
            new KeyValuePair<string, string?>($"ClientAddressing:TrustedProxies:{index}", address)))
        .Build();
}

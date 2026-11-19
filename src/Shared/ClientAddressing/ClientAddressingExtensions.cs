using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HalsingeLive.ClientAddressing;

public static class ClientAddressingExtensions
{
    public static IServiceCollection AddClientAddressing(
        this IServiceCollection services, IConfiguration configuration)
    {
        var entries = configuration.GetSection("ClientAddressing:TrustedProxies").Get<string[]>() ?? [];
        var proxies = entries.Select(entry => IPAddress.TryParse(entry, out var address)
            ? address
            : throw new InvalidOperationException(
                "ClientAddressing:TrustedProxies must contain individual IP addresses.")).ToArray();

        // Minimal hosting can otherwise add another forwarding middleware outside
        // this pipeline, consuming a second hop despite ForwardLimit = 1.
        configuration["ForwardedHeaders_Enabled"] = "false";

        // Own all forwarding options rather than inheriting permissive host defaults.
        // Empty allowlists must disable forwarding: clearing both known lists alone trusts everyone.
        services.PostConfigure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = proxies.Length == 0 ? ForwardedHeaders.None
                : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
                if (proxy.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    options.KnownProxies.Add(proxy.MapToIPv6());
            }
        });
        return services;
    }

    public static IApplicationBuilder UseClientAddressing(this IApplicationBuilder app)
    {
        app.UseForwardedHeaders();
        return app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = NormalizeClientAddress(context.Connection.RemoteIpAddress);
            // Consumers use RemoteIpAddress only. Never leave alternate or unconsumed claims
            // available to downstream code. YARP generates fresh headers from this context.
            foreach (var name in ForwardingHeaders)
                context.Request.Headers.Remove(name);
            await next(context);
        });
    }

    public static IPAddress? NormalizeClientAddress(IPAddress? address)
    {
        if (address is null)
            return null;
        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4();
        // IPv6 scope IDs describe a local interface, not a distinct client identity.
        return new IPAddress(address.GetAddressBytes());
    }

    private static readonly string[] ForwardingHeaders =
    [
        "Forwarded", "X-Real-IP", "X-Forwarded-For", "X-Forwarded-Proto",
        "X-Forwarded-Host", "X-Forwarded-Prefix", "X-Original-For",
        "X-Original-Proto", "X-Original-Host", "X-Original-Prefix"
    ];
}

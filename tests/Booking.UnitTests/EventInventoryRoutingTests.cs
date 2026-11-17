using Booking.Features.EventInventory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.UnitTests;

public sealed class EventInventoryRoutingTests
{
    [Fact]
    public async Task Routes_ExcludeRawCreationAndRetainCatalogCreation()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(EventInventoryController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        Assert.DoesNotContain(endpoints, endpoint =>
            endpoint.RoutePattern.RawText?.TrimStart('/') == "CreateEventInventory/{eventId:guid}");
        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText?.TrimStart('/') ==
                "CreateEventInventoryFromCatalog/from-catalog/{eventId:guid}"
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Post) == true);
    }
}

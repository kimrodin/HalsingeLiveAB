using Booking.Data;
using Booking.Features.EventInventory;
using Booking.Features.EventInventory.CreateEventInventory;
using Booking.Features.EventInventory.GetEventInventory;
using Booking.Features.EventInventory.ListEventInventory;
using Booking.Features.EventInventory.EventCatalog;
using Booking.Features.Seed;
using Booking.Features.EventCatalog;
using Booking.Features.Payments;
using Booking.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Booking")));
builder.Services.AddHttpClient<IEventSnapshotProvider, EventCatalogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["EventCatalog:BaseUrl"]
        ?? throw new InvalidOperationException("EventCatalog base URL is missing."));
});
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Redis connection string is missing.");
    return ConnectionMultiplexer.Connect(connectionString);
});
builder.Services.AddSingleton<Booking.Features.BookingHolds.RedisReservationService>();
builder.Services.AddScoped<Booking.Features.Bookings.BookingService>();
builder.Services.AddHttpClient<Booking.Features.Bookings.VenueOccupancyReport>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["EventCatalog:BaseUrl"]
        ?? throw new InvalidOperationException("EventCatalog base URL is missing."));
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("EventCatalog:TimeoutSeconds", 5));
});
builder.Services.AddHostedService<Booking.Features.Bookings.BookingExpirationService>();
builder.Services.AddHostedService<Booking.Features.Bookings.PaymentReconciliationService>();
builder.Services.AddHttpClient<IPaymentGateway, HttpPaymentGateway>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PaymentSimulator:BaseUrl"]
        ?? throw new InvalidOperationException("PaymentSimulator base URL is missing."));
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("PaymentSimulator:TimeoutSeconds", 5));
});
builder.Services.AddHttpClient<ITicketServiceClient, HttpTicketServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["TicketService:BaseUrl"]
        ?? throw new InvalidOperationException("TicketService base URL is missing."));
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("TicketService:TimeoutSeconds", 5));
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TicketIssueOutboxDispatcher>();
builder.Services.AddHostedService<TicketIssueOutboxBackgroundService>();
builder.Services.AddScoped<CreateEventInventoryHandler>();
builder.Services.AddScoped<CreateEventInventoryFromCatalogHandler>();
builder.Services.AddHttpClient<IEventCatalogClient, HttpEventCatalogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["EventCatalog:BaseUrl"]
        ?? throw new InvalidOperationException("EventCatalog base URL is missing."));
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("EventCatalog:TimeoutSeconds", 5));
});
builder.Services.AddScoped<GetEventInventoryHandler>();
builder.Services.AddScoped<ListEventInventoryHandler>();
builder.Services.AddScoped<SeedInventoryHandler>();
builder.Services.AddScoped<DeleteSeedInventoryHandler>();
builder.Services.AddScoped<ClearBookingDatabaseHandler>();
builder.Services.AddControllers();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"]
            ?? "http://localhost:8180/realms/halsingelive";
        if (builder.Configuration["Authentication:MetadataAddress"] is { Length: > 0 } metadataAddress)
            options.MetadataAddress = metadataAddress;
        options.Audience = "eventcatalog";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = options.Authority.TrimEnd('/'),
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("admin");
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    if (builder.Configuration.GetValue<bool>("InitializeDatabase"))
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

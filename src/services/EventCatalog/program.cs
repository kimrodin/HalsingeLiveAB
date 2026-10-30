using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using EventCatalog.Data;
using EventCatalog.Features.Events.Images;
using EventCatalog.Features.Events.CreateEvent;
using EventCatalog.Features.Events.GetEvent;
using EventCatalog.Features.Events.DeleteEvent;
using EventCatalog.Features.Events.ListEvents;
using EventCatalog.Features.Events.UpdateEvent;
using EventCatalog.Features.Venues.CreateVenue;
using EventCatalog.Features.Venues.CreateSection;
using EventCatalog.Features.Venues.DeleteSection;
using EventCatalog.Features.Venues.DeleteVenue;
using EventCatalog.Features.Venues.GetVenue;
using EventCatalog.Features.Venues.ListVenues;
using EventCatalog.Features.Venues.UpdateVenue;
using EventCatalog.Features.Venues.UpdateSection;
using EventCatalog.Features.Venues.SeatingMaps;
using EventCatalog.Features.Seed;
using EventCatalog.Features.Seed.SeedEvents;
using EventCatalog.Features.Seed.SeedVenues;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EventCatalogDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("EventCatalog")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<EventImageStorageOptions>(builder.Configuration.GetSection("EventImageStorage"));
builder.Services.AddSingleton<IEventImageStorage, LocalEventImageStorage>();
builder.Services.AddScoped<UploadEventImageHandler>();
builder.Services.AddScoped<GetEventImageHandler>();
builder.Services.AddScoped<DeleteEventImageHandler>();
builder.Services.AddScoped<GetEventHandler>();
builder.Services.AddScoped<CreateEventHandler>();
builder.Services.AddHttpClient<IBookingInventoryClient, HttpBookingInventoryClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Booking:BaseUrl"]
        ?? throw new InvalidOperationException("Booking base URL is missing."));
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Booking:TimeoutSeconds", 5));
});
builder.Services.AddScoped<DeleteEventHandler>();
builder.Services.AddScoped<ListEventsHandler>();
builder.Services.AddScoped<UpdateEventHandler>();
builder.Services.AddScoped<CreateVenueHandler>();
builder.Services.AddScoped<CreateSectionHandler>();
builder.Services.AddScoped<DeleteSectionHandler>();
builder.Services.AddScoped<DeleteVenueHandler>();
builder.Services.AddScoped<GetVenueHandler>();
builder.Services.AddScoped<ListVenuesHandler>();
builder.Services.AddScoped<UpdateVenueHandler>();
builder.Services.AddScoped<UpdateSectionHandler>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"]
            ?? "http://localhost:8180/realms/halsingelive";
        // Containers fetch metadata and signing keys through Keycloak's internal
        // backchannel; tokens still use the public Authority as their issuer.
        if (builder.Configuration["Authentication:MetadataAddress"] is { Length: > 0 } metadataAddress)
            options.MetadataAddress = metadataAddress;
        options.Audience = "eventcatalog";

        // HTTP tillåts bara för den lokala utvecklingsmiljön.
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
builder.Services.Configure<VenueMapStorageOptions>(builder.Configuration.GetSection("VenueMapStorage"));
builder.Services.AddSingleton<IVenueSeatingMapStorage, LocalVenueSeatingMapStorage>();
builder.Services.AddScoped<UploadVenueSeatingMapHandler>();
builder.Services.AddScoped<ListVenueSeatingMapsHandler>();
builder.Services.AddScoped<GetVenueSeatingMapHandler>();
builder.Services.AddScoped<DeleteVenueSeatingMapHandler>();
builder.Services.AddScoped<SeedVenuesHandler>();
builder.Services.AddScoped<SeedEventsHandler>();
builder.Services.AddScoped<SeedAllHandler>();
builder.Services.AddScoped<DeleteSeedDataHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Apply pending EF Core migrations to the local database when explicitly enabled.
    if (builder.Configuration.GetValue<bool>("InitializeDatabase"))
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventCatalogDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Endpoints
// --------------------------------------------------

// Event endpoints will be registered here

app.Run();

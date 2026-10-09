using Microsoft.EntityFrameworkCore;
using EventCatalog.Data;
using EventCatalog.Features.Events.CreateEvent;
using EventCatalog.Features.Events.GetEvent;
using EventCatalog.Features.Events.DeleteEvent;
using EventCatalog.Features.Events.ListEvents;
using EventCatalog.Features.Venues.CreateVenue;
using EventCatalog.Features.Venues.DeleteVenue;
using EventCatalog.Features.Venues.GetVenue;
using EventCatalog.Features.Venues.ListVenues;
using EventCatalog.Features.Venues.UpdateVenue;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EventCatalogDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("EventCatalog")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<GetEventHandler>();
builder.Services.AddScoped<CreateEventHandler>();
builder.Services.AddScoped<DeleteEventHandler>();
builder.Services.AddScoped<ListEventsHandler>();
builder.Services.AddScoped<CreateVenueHandler>();
builder.Services.AddScoped<DeleteVenueHandler>();
builder.Services.AddScoped<GetVenueHandler>();
builder.Services.AddScoped<ListVenuesHandler>();
builder.Services.AddScoped<UpdateVenueHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Opt-in initialization for an empty local test database, not existing schemas.
    if (builder.Configuration.GetValue<bool>("InitializeDatabase"))
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventCatalogDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

// Endpoints
// --------------------------------------------------

// Event endpoints will be registered here

app.Run();

using Microsoft.EntityFrameworkCore;
using EventCatalog.Data;
using EventCatalog.Features.Events.CreateEvent;
using EventCatalog.Features.Events.GetEvent;
using EventCatalog.Features.Events.DeleteEvent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EventCatalogDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("EventCatalog")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<GetEventHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

// Endpoints
// --------------------------------------------------

// Event endpoints will be registered here

app.Run();
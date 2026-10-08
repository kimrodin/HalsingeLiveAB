using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using EventCatalog.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EventCatalogDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("EventCatalog")));

var app = builder.Build();

app.UseHttpsRedirection();

// Endpoints
// --------------------------------------------------

// Event endpoints will be registered here

app.Run();
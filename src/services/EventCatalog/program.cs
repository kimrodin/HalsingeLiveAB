using Microsoft.EntityFrameworkCore;
using EventCatalog.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EventCatalogDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("EventCatalog")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();

// Endpoints
// --------------------------------------------------

// Event endpoints will be registered here

app.Run();
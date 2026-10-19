using Booking.Data;
using Booking.Features.EventInventory;
using Booking.Features.EventInventory.CreateEventInventory;
using Booking.Features.EventInventory.GetEventInventory;
using Booking.Features.EventInventory.ListEventInventory;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Booking")));
builder.Services.AddScoped<CreateEventInventoryHandler>();
builder.Services.AddScoped<GetEventInventoryHandler>();
builder.Services.AddScoped<ListEventInventoryHandler>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    if (builder.Configuration.GetValue<bool>("InitializeDatabase"))
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

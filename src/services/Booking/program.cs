using Booking.Data;
using Booking.Features.EventInventory;
using Booking.Features.EventInventory.CreateEventInventory;
using Booking.Features.EventInventory.GetEventInventory;
using Booking.Features.EventInventory.ListEventInventory;
using Booking.Features.Seed;
using Booking.Features.Payments;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Booking")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Redis connection string is missing.");
    return ConnectionMultiplexer.Connect(connectionString);
});
builder.Services.AddSingleton<Booking.Features.BookingHolds.RedisReservationService>();
builder.Services.AddScoped<Booking.Features.Bookings.BookingService>();
builder.Services.AddHostedService<Booking.Features.Bookings.BookingExpirationService>();
builder.Services.AddHostedService<Booking.Features.Bookings.PaymentReconciliationService>();
builder.Services.AddHttpClient<IPaymentGateway, HttpPaymentGateway>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PaymentSimulator:BaseUrl"]
        ?? throw new InvalidOperationException("PaymentSimulator base URL is missing."));
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("PaymentSimulator:TimeoutSeconds", 5));
});
builder.Services.AddScoped<CreateEventInventoryHandler>();
builder.Services.AddScoped<GetEventInventoryHandler>();
builder.Services.AddScoped<ListEventInventoryHandler>();
builder.Services.AddScoped<SeedInventoryHandler>();
builder.Services.AddScoped<DeleteSeedInventoryHandler>();
builder.Services.AddScoped<ClearBookingDatabaseHandler>();
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
        await dbContext.Database.MigrateAsync();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

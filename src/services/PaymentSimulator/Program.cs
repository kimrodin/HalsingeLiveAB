using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.GetPayment;
using PaymentSimulator.Features.Payments.ProcessPayment;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PaymentSimulator")
    ?? throw new InvalidOperationException(
        "Connection string 'PaymentSimulator' is not configured.");

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<GetPaymentHandler>();
builder.Services.AddScoped<ProcessPaymentHandler>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PaymentSimulator.Authentication;
using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.GetPayment;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Payments.RefundPayment;
using PaymentSimulator.Features.Payments.Simulation;
using PaymentSimulator.Messaging;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PaymentSimulator")
    ?? throw new InvalidOperationException(
        "Connection string 'PaymentSimulator' is not configured.");

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>("postgresql", tags: ["ready"]);
builder.Services.Configure<HealthCheckServiceOptions>(options =>
    options.Registrations.Single(check => check.Name == "postgresql").Timeout = TimeSpan.FromSeconds(3));

builder.Services.AddScoped<PaymentSimulator.Features.Seed.SeedPaymentsHandler>();
builder.Services.AddScoped<GetPaymentHandler>();
builder.Services.AddScoped<ProcessPaymentHandler>();
builder.Services.AddScoped<RefundPaymentHandler>();
builder.Services.AddScoped<SimulationSettingsService>();
builder.Services.AddHostedService<PaymentRecoveryWorker>();
builder.Services.AddSingleton<RabbitMqConnectionProvider>();
builder.Services.AddScoped<PaymentRequestedHandler>();
builder.Services.AddHostedService<PaymentRequestedConsumer>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPaymentResultPublisher, RabbitMqPaymentResultPublisher>();
builder.Services.AddScoped<PaymentResultOutboxDispatcher>();
builder.Services.AddHostedService<PaymentResultOutboxWorker>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddAdminAuthentication(builder.Configuration, builder.Environment.IsDevelopment());

var app = builder.Build();

{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await PaymentDatabaseMigration.MigrateAsync(dbContext);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var readinessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse,
    ResultStatusCodes =
    {
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable
    }
};
app.MapHealthChecks("/health", readinessOptions).AllowAnonymous();
app.MapHealthChecks("/health/ready", readinessOptions).AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

app.Run();

static Task WriteHealthResponse(HttpContext context, HealthReport report) =>
    context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Status.ToString())
    }, cancellationToken: context.RequestAborted);

public partial class Program;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using TicketService.Data;
using TicketService.Features.Tickets.DownloadTicket;
using TicketService.Features.Tickets.IssueTickets;
using TicketService.Infrastructure.Messaging;
using TicketService.Messaging.Issuance;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((_, loggerConfiguration) =>
    loggerConfiguration
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "TicketService")
        .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
        .WriteTo.Console()
        .WriteTo.Seq(builder.Configuration["Serilog:SeqServerUrl"] ?? "http://localhost:5341"));

QuestPDF.Settings.License = LicenseType.Community;

var connectionString = builder.Configuration.GetConnectionString("TicketService")
    ?? throw new InvalidOperationException(
        "Connection string 'TicketService' is not configured.");

builder.Services.AddDbContext<TicketDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TicketDbContext>("postgresql", tags: ["ready"]);
builder.Services.Configure<HealthCheckServiceOptions>(options =>
    options.Registrations.Single(check => check.Name == "postgresql").Timeout = TimeSpan.FromSeconds(3));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TicketDocumentService>();
builder.Services.AddScoped<IssueTicketsHandler>();
builder.Services.AddSingleton<RabbitMqConnectionProvider>();
builder.Services.AddHostedService<BookingConfirmedConsumer>();
builder.Services.AddControllers();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy =>
{
    policy.RequireAuthenticatedUser();
    policy.RequireRole("admin");
}));

var app = builder.Build();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("InitializeDatabase"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
    await dbContext.Database.MigrateAsync();
}

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

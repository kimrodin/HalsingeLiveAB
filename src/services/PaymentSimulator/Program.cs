using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.GetPayment;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Payments.RefundPayment;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PaymentSimulator")
    ?? throw new InvalidOperationException(
        "Connection string 'PaymentSimulator' is not configured.");

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<PaymentSimulator.Features.Seed.SeedPaymentsHandler>();
builder.Services.AddScoped<GetPaymentHandler>();
builder.Services.AddScoped<ProcessPaymentHandler>();
builder.Services.AddScoped<RefundPaymentHandler>();
builder.Services.AddHostedService<PaymentRecoveryWorker>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
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
        ValidIssuer = options.Authority.TrimEnd('/'), NameClaimType = "preferred_username", RoleClaimType = "roles"
    };
});
builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy =>
    policy.RequireAuthenticatedUser().RequireRole("admin")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

var app = builder.Build();

{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await PaymentDatabaseMigration.MigrateAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;

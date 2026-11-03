using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using TicketService.Data;
using TicketService.Features.Tickets.DownloadTicket;
using TicketService.Features.Tickets.IssueTickets;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

var connectionString = builder.Configuration.GetConnectionString("TicketService")
    ?? throw new InvalidOperationException(
        "Connection string 'TicketService' is not configured.");

builder.Services.AddDbContext<TicketDbContext>(options =>
    options.UseNpgsql(connectionString));
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("InitializeDatabase"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;

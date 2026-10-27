using Microsoft.EntityFrameworkCore;
using TicketService.Data;
using TicketService.Features.Tickets.IssueTickets;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TicketService")
    ?? throw new InvalidOperationException(
        "Connection string 'TicketService' is not configured.");

builder.Services.AddDbContext<TicketDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IssueTicketsHandler>();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("InitializeDatabase"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapControllers();

app.Run();

public partial class Program;

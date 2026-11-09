using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((_, loggerConfiguration) =>
    loggerConfiguration.Enrich.FromLogContext().WriteTo.Console());

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.UseSerilogRequestLogging();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.UseWebSockets();
app.MapReverseProxy();

app.Run();

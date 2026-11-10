using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((_, loggerConfiguration) =>
    loggerConfiguration
        .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
        .MinimumLevel.Override("Yarp.ReverseProxy.Forwarder.HttpForwarder", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "ApiGateway")
        .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
        .WriteTo.Console()
        .WriteTo.Seq(builder.Configuration["Serilog:SeqServerUrl"] ?? "http://localhost:5341"));

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, context) =>
    {
        if (Guid.TryParse(context.Response.Headers["X-Correlation-ID"].ToString(), out var correlationId))
        {
            diagnosticContext.Set("CorrelationId", correlationId);
            diagnosticContext.Set("BookingId", correlationId);
        }
    };
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.UseWebSockets();
app.MapReverseProxy();

app.Run();

using HalsingeLive.ClientAddressing;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddClientAddressing(builder.Configuration);
var app = builder.Build();
// Same middleware and configuration as Booking, without its databases/background workers.
app.UseClientAddressing();
app.MapGet("/probe", (HttpContext context) => new
{
    address = context.Connection.RemoteIpAddress?.ToString(),
    scheme = context.Request.Scheme,
    hasForwardingHeaders = context.Request.Headers.Keys.Any(key =>
        key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)
        || key.Equals("X-Real-IP", StringComparison.OrdinalIgnoreCase)
        || key.Equals("Forwarded", StringComparison.OrdinalIgnoreCase))
});
app.Run();

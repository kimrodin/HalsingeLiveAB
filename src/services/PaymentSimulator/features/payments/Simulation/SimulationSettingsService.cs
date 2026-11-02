using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.Models;
using PaymentSimulator.Features.Payments.ProcessPayment;

namespace PaymentSimulator.Features.Payments.Simulation;

public sealed class SimulationSettings
{
    public int Id { get; set; } = 1;
    public PaymentStatus Outcome { get; set; }
    public int DelayMs { get; set; }
    public int TimeoutMs { get; set; }
}

public sealed class SimulationSettingsService(PaymentDbContext db, IConfiguration configuration)
{
    public async Task<PaymentSimulationSettings> Get(CancellationToken cancellationToken)
    {
        var settings = await db.SimulationSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return settings is null
            ? PaymentValidation.ReadSimulationSettings(configuration)
            : new(settings.Outcome, settings.DelayMs, settings.TimeoutMs);
    }

    public async Task<PaymentSimulationSettings> Update(PaymentSimulationSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.Outcome is not (PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.TimedOut))
            throw new ArgumentException("Outcome must be Succeeded, Failed or TimedOut.");
        if (settings.DelayMs < 0 || settings.TimeoutMs is < 1 or > 30000)
            throw new ArgumentException("DelayMs must be non-negative; TimeoutMs must be 1..30000.");

        // A single statement also handles concurrent first updates across service instances.
        var outcome = settings.Outcome.ToString();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SimulationSettings" ("Id", "Outcome", "DelayMs", "TimeoutMs")
            VALUES (1, {outcome}, {settings.DelayMs}, {settings.TimeoutMs})
            ON CONFLICT ("Id") DO UPDATE SET
                "Outcome" = EXCLUDED."Outcome", "DelayMs" = EXCLUDED."DelayMs",
                "TimeoutMs" = EXCLUDED."TimeoutMs"
            """, cancellationToken);
        return settings;
    }
}

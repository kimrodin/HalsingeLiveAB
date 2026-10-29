using PaymentSimulator.Features.Payments.Models;

namespace PaymentSimulator.Features.Payments.ProcessPayment;

public static class PaymentValidation
{
    public static string ValidateAndNormalizeCurrency(ProcessPaymentRequest request)
    {
        if (request.BookingId == Guid.Empty)
            throw new ArgumentException("Invalid BookingId");
        if (request.Amount <= 0 || request.Amount != decimal.Round(request.Amount, 2)
            || request.Amount > 9999999999999999.99m)
            throw new ArgumentException("Amount must be positive and have at most two decimal places.");
        var currency = request.Currency?.Trim().ToUpperInvariant();
        if (currency is null || currency.Length != 3 || !currency.All(char.IsAsciiLetter))
            throw new ArgumentException("Currency must be a three-letter code.");
        return currency;
    }

    public static PaymentSimulationSettings ReadSimulationSettings(IConfiguration configuration)
    {
        var outcomeText = configuration["PaymentSimulation:Outcome"] ?? "Succeeded";
        var outcome = outcomeText.ToLowerInvariant() switch
        {
            "succeeded" => PaymentStatus.Succeeded,
            "failed" => PaymentStatus.Failed,
            "timeout" or "timedout" => PaymentStatus.TimedOut,
            _ => throw new InvalidOperationException($"Unknown payment outcome: {outcomeText}")
        };
        var delayMs = configuration.GetValue("PaymentSimulation:DelayMs", 500);
        var timeoutMs = configuration.GetValue("PaymentSimulation:TimeoutMs", 5000);
        if (delayMs < 0 || timeoutMs <= 0 || timeoutMs > 30000)
            throw new InvalidOperationException("DelayMs must be non-negative; TimeoutMs must be 1..30000.");
        return new(outcome, delayMs, timeoutMs);
    }
}

public sealed record PaymentSimulationSettings(PaymentStatus Outcome, int DelayMs, int TimeoutMs);

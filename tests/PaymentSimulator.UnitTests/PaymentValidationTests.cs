using System.Globalization;
using Microsoft.Extensions.Configuration;
using PaymentSimulator.Features.Payments.Models;
using PaymentSimulator.Features.Payments.ProcessPayment;

namespace PaymentSimulator.UnitTests;

public sealed class PaymentValidationTests
{
    private static ProcessPaymentRequest Request(decimal amount = 125.50m, string currency = "SEK") =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), amount, currency);

    [Theory]
    [InlineData("0.01")]
    [InlineData("9999999999999999.99")]
    public void Validate_AcceptsSmallestAndLargestAllowedAmount(string amount) =>
        Assert.Equal("SEK", PaymentValidation.ValidateAndNormalizeCurrency(
            Request(decimal.Parse(amount, CultureInfo.InvariantCulture))));

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("10000000000000000")]
    public void Validate_RejectsNonPositiveOverpreciseOrExcessiveAmount(string amount)
    {
        var exception = Assert.Throws<ArgumentException>(() => PaymentValidation.ValidateAndNormalizeCurrency(
            Request(decimal.Parse(amount, CultureInfo.InvariantCulture))));
        Assert.Contains("Amount", exception.Message);
    }

    [Fact]
    public void Validate_RejectsEmptyBookingId()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            PaymentValidation.ValidateAndNormalizeCurrency(Request() with { BookingId = Guid.Empty }));
        Assert.Contains("BookingId", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("S3K")]
    public void Validate_RejectsMissingOrNonAlphabeticCurrency(string? currency)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            PaymentValidation.ValidateAndNormalizeCurrency(Request(currency: currency!)));
        Assert.Contains("Currency", exception.Message);
    }

    [Fact]
    public void Validate_TrimsAndUppercasesCurrency() =>
        Assert.Equal("SEK", PaymentValidation.ValidateAndNormalizeCurrency(Request(currency: " sek ")));

    [Fact]
    public void ReadSimulationSettings_UsesDefaultOutcomeDelayAndTimeout()
    {
        var settings = PaymentValidation.ReadSimulationSettings(new ConfigurationBuilder().Build());
        Assert.Equal(PaymentStatus.Succeeded, settings.Outcome);
        Assert.Equal(500, settings.DelayMs);
        Assert.Equal(5000, settings.TimeoutMs);
    }

    [Theory]
    [InlineData(-1, 5000)]
    [InlineData(0, 0)]
    [InlineData(0, 30001)]
    public void ReadSimulationSettings_RejectsNegativeDelayOrTimeoutOutsideLimits(int delayMs, int timeoutMs)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaymentSimulation:DelayMs"] = delayMs.ToString(CultureInfo.InvariantCulture),
            ["PaymentSimulation:TimeoutMs"] = timeoutMs.ToString(CultureInfo.InvariantCulture)
        }).Build();
        var exception = Assert.Throws<InvalidOperationException>(() => PaymentValidation.ReadSimulationSettings(configuration));
        Assert.Contains("TimeoutMs", exception.Message);
    }

    [Fact]
    public void ReadSimulationSettings_RejectsUnknownOutcome()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaymentSimulation:Outcome"] = "unknown"
        }).Build();
        var exception = Assert.Throws<InvalidOperationException>(() => PaymentValidation.ReadSimulationSettings(configuration));
        Assert.Contains("Unknown payment outcome", exception.Message);
    }
}

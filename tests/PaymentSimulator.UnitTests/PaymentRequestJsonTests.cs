using System.Text.Json;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Seed;

namespace PaymentSimulator.UnitTests;

public sealed class PaymentRequestJsonTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("")]
    [InlineData(",\"amount\":null")]
    public void Payment_request_rejects_missing_or_null_amount(string amountField)
    {
        var json = "{\"bookingId\":\"11111111-1111-1111-1111-111111111111\",\"currency\":\"SEK\""
            + amountField + "}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProcessPaymentRequest>(json, Options));
    }

    [Fact]
    public void Payment_request_accepts_explicit_zero_amount()
    {
        const string json = """
            {"bookingId":"11111111-1111-1111-1111-111111111111","amount":0,"currency":"SEK"}
            """;

        var request = JsonSerializer.Deserialize<ProcessPaymentRequest>(json, Options)!;

        Assert.Equal(0m, request.Amount);
        Assert.Equal("SEK", PaymentValidation.ValidateAndNormalizeCurrency(request));
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"amount\":null")]
    public void Seed_request_rejects_missing_or_null_amount(string amountField)
    {
        var json = "[{\"bookingId\":\"51000000-0001-0001-0000-000000000001\",\"paidAt\":\"2026-11-18T12:00:00Z\""
            + amountField + "}]";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SeedPayment[]>(json, Options));
    }

    [Fact]
    public void Seed_request_accepts_explicit_zero_amount()
    {
        const string json = """
            [{"bookingId":"51000000-0001-0001-0000-000000000001","amount":0,"paidAt":"2026-11-18T12:00:00Z"}]
            """;

        var rows = JsonSerializer.Deserialize<SeedPayment[]>(json, Options)!;

        Assert.Equal(0m, Assert.Single(rows).Amount);
    }
}

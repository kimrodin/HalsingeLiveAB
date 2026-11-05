using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Payments.RefundPayment;
using PaymentSimulator.Features.Payments.Simulation;
using PaymentSimulator.Messaging;

namespace PaymentSimulator.UnitTests;

public sealed class PaymentRequestedHandlerTests
{
    public static IEnumerable<object[]> InvalidRequests()
    {
        var message = CreateMessage();
        yield return [message with { MessageId = Guid.Empty }, "MessageId"];
        yield return [message with { CorrelationId = Guid.Empty }, "CorrelationId"];
        yield return [message with { OccurredAtUtc = default }, "OccurredAtUtc"];
        yield return [message with { BookingId = Guid.Empty }, "BookingId"];
        yield return [message with { Amount = 0m }, "Amount"];
        yield return [message with { Amount = 1.001m }, "Amount"];
        foreach (var currency in new[] { null, "", "sek", " SEK ", "S3K" })
            yield return [message with { Currency = currency! }, "Currency"];
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task HandleAsync_RejectsInvalidRequestsBeforeAccessingDatabase(PaymentRequestedV1 message, string field)
    {
        // No provider is configured: reaching the database would fail this validation test.
        await using var db = new PaymentDbContext(new DbContextOptions<PaymentDbContext>());
        var handler = CreateHandler(db);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(message));

        Assert.Contains(field, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_RejectsNullRequest()
    {
        await using var db = new PaymentDbContext(new DbContextOptions<PaymentDbContext>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateHandler(db).HandleAsync(null!));
    }

    private static PaymentRequestedHandler CreateHandler(PaymentDbContext db) => new(new ProcessPaymentHandler(
        db, new SimulationSettingsService(db, new ConfigurationBuilder().Build()), new RefundPaymentHandler(db)));

    private static PaymentRequestedV1 CreateMessage() => new()
    {
        MessageId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), BookingId = Guid.NewGuid(),
        Amount = 125.50m, Currency = "SEK", OccurredAtUtc = DateTimeOffset.UtcNow
    };
}

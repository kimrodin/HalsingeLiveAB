using System.Text.Json;
using HalsingeLive.Contracts.Messaging;
using static TicketService.UnitTests.TicketTestData;

namespace TicketService.UnitTests;

public sealed class BookingConfirmedContractTests
{
    [Theory]
    [InlineData(nameof(BookingConfirmed.MessageId))]
    [InlineData(nameof(BookingConfirmed.CorrelationId))]
    [InlineData(nameof(BookingConfirmed.BookingId))]
    [InlineData(nameof(BookingConfirmed.BookingItemId))]
    [InlineData(nameof(BookingConfirmed.EventId))]
    [InlineData(nameof(BookingConfirmed.EventName))]
    [InlineData(nameof(BookingConfirmed.EventStartsAtUtc))]
    [InlineData(nameof(BookingConfirmed.VenueName))]
    [InlineData(nameof(BookingConfirmed.SectionName))]
    [InlineData(nameof(BookingConfirmed.Quantity))]
    [InlineData(nameof(BookingConfirmed.UnitPrice))]
    [InlineData(nameof(BookingConfirmed.Currency))]
    [InlineData(nameof(BookingConfirmed.OccurredAtUtc))]
    public void Deserialize_RejectsMissingFieldForRabbitMqAndHttp(string field)
    {
        foreach (var options in new[] { JsonSerializerOptions.Default, JsonSerializerOptions.Web })
        {
            var payload = JsonSerializer.SerializeToNode(CreateMessage(), options)!.AsObject();
            var propertyName = options.PropertyNamingPolicy?.ConvertName(field) ?? field;
            Assert.True(payload.Remove(propertyName));

            var exception = Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BookingConfirmed>(payload.ToJsonString(), options));

            Assert.Contains(propertyName, exception.Message);
        }
    }

    [Theory]
    [InlineData("SEK")]
    [InlineData("EUR")]
    public void Serialize_RoundTripsExplicitZeroPriceAndCurrencyForRabbitMqAndHttp(string currency)
    {
        var message = CreateMessage() with { UnitPrice = 0m, Currency = currency };

        foreach (var options in new[] { JsonSerializerOptions.Default, JsonSerializerOptions.Web })
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(message, options);

            Assert.Equal(message, JsonSerializer.Deserialize<BookingConfirmed>(payload, options));
        }
    }
}

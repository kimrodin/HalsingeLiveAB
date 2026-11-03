using System.Text.Json;
using HalsingeLive.Contracts.Messaging;
using static TicketService.UnitTests.TicketTestData;

namespace TicketService.UnitTests;

public sealed class BookingConfirmedContractTests
{
    [Theory]
    [InlineData(nameof(BookingConfirmedV1.MessageId))]
    [InlineData(nameof(BookingConfirmedV1.CorrelationId))]
    [InlineData(nameof(BookingConfirmedV1.BookingId))]
    [InlineData(nameof(BookingConfirmedV1.BookingItemId))]
    [InlineData(nameof(BookingConfirmedV1.EventId))]
    [InlineData(nameof(BookingConfirmedV1.EventName))]
    [InlineData(nameof(BookingConfirmedV1.EventStartsAtUtc))]
    [InlineData(nameof(BookingConfirmedV1.VenueName))]
    [InlineData(nameof(BookingConfirmedV1.SectionName))]
    [InlineData(nameof(BookingConfirmedV1.Quantity))]
    [InlineData(nameof(BookingConfirmedV1.UnitPrice))]
    [InlineData(nameof(BookingConfirmedV1.Currency))]
    [InlineData(nameof(BookingConfirmedV1.OccurredAtUtc))]
    public void Deserialize_RejectsMissingFieldForRabbitMqAndHttp(string field)
    {
        foreach (var options in new[] { JsonSerializerOptions.Default, JsonSerializerOptions.Web })
        {
            var payload = JsonSerializer.SerializeToNode(CreateMessage(), options)!.AsObject();
            var propertyName = options.PropertyNamingPolicy?.ConvertName(field) ?? field;
            Assert.True(payload.Remove(propertyName));

            var exception = Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BookingConfirmedV1>(payload.ToJsonString(), options));

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

            Assert.Equal(message, JsonSerializer.Deserialize<BookingConfirmedV1>(payload, options));
        }
    }
}

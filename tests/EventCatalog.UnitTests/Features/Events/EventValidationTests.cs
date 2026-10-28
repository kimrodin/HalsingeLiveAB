using EventCatalog.Features.Events;
using EventCatalog.Features.Events.CreateEvent;
using EventCatalog.Features.Events.UpdateEvent;

namespace EventCatalog.UnitTests.Features.Events;

public sealed class EventValidationTests
{
    private static readonly DateTime Start = new(2030, 6, 1, 18, 0, 0, DateTimeKind.Utc);
    private static readonly Guid VenueId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SectionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static CreateEventRequest ValidRequest() => new(
        "Concert", "An evening of music", VenueId, Start, Start.AddHours(2), Start.AddDays(-30),
        [new(SectionId, 125m)]);

    [Fact]
    public void ValidateCreateEvent_AcceptsMaximumTextLengthsZeroPriceAndInclusiveSaleDate() =>
        EventValidation.ValidateCreateEvent(ValidRequest() with
        {
            Name = new string('N', 200), Description = new string('D', 2000),
            OnSaleFrom = Start, SectionPrices = [new(SectionId, 0m)]
        });

    [Theory]
    [InlineData("blank-name", "Name")]
    [InlineData("overlong-name", "Name")]
    [InlineData("end-at-start", "EndDate")]
    [InlineData("sale-after-start", "OnSaleFrom")]
    [InlineData("default-start", "StartDate")]
    [InlineData("default-end", "EndDate")]
    [InlineData("default-sale", "OnSaleFrom")]
    [InlineData("duplicate-section", "SectionPrices")]
    [InlineData("negative-price", "SectionPrices")]
    public void ValidateCreateEvent_RejectsInvalidField(string scenario, string field)
    {
        var valid = ValidRequest();
        var request = scenario switch
        {
            "blank-name" => valid with { Name = "  " },
            "overlong-name" => valid with { Name = new string('N', 201) },
            "end-at-start" => valid with { EndDate = Start },
            "sale-after-start" => valid with { OnSaleFrom = Start.AddTicks(1) },
            "default-start" => valid with { StartDate = default(DateTime) },
            "default-end" => valid with { EndDate = default(DateTime) },
            "default-sale" => valid with { OnSaleFrom = default(DateTime) },
            "duplicate-section" => valid with { SectionPrices = [new(SectionId, 10m), new(SectionId, 20m)] },
            "negative-price" => valid with { SectionPrices = [new(SectionId, -0.01m)] },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var exception = Assert.Throws<EventValidationException>(() => EventValidation.ValidateCreateEvent(request));
        Assert.Contains(field, exception.Errors.Keys);
        Assert.NotEmpty(exception.Errors[field]);
    }

    [Fact]
    public void ValidateUpdateEvent_RejectsEndDateEqualToStartDate()
    {
        var request = new UpdateEventRequest("Concert", "Music", Start, Start, VenueId,
            Start.AddDays(-30), [new(SectionId, 125m)]);
        Assert.Contains("EndDate", Assert.Throws<EventValidationException>(
            () => EventValidation.ValidateUpdateEvent(request)).Errors.Keys);
    }
}

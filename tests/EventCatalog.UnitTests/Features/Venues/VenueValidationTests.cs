using EventCatalog.Features.Venues;
using EventCatalog.Features.Venues.Models;

namespace EventCatalog.UnitTests.Features.Venues;

public sealed class VenueValidationTests
{
    [Fact]
    public void ValidateVenue_AcceptsMinimumCapacityAndMaximumTextLengths() =>
        VenueValidation.ValidateVenue(new string('N', 200), new string('S', 500),
            new string('P', 20), new string('C', 100), 1);

    [Fact]
    public void ValidateVenue_RejectsZeroCapacity() =>
        Assert.Contains("Capacity", Assert.Throws<VenueValidationException>(() =>
            VenueValidation.ValidateVenue("Arena", "Street 1", "12345", "Hudiksvall", 0)).Errors.Keys);

    [Fact]
    public void ValidateSection_RejectsZeroCapacity() =>
        Assert.Contains("Capacity", Assert.Throws<VenueValidationException>(() =>
            VenueValidation.ValidateSection("Floor", 0, SeatingType.GeneralAdmission)).Errors.Keys);

    [Fact]
    public void ValidateSection_RejectsUndefinedSeatingType() =>
        Assert.Contains("SeatingType", Assert.Throws<VenueValidationException>(() =>
            VenueValidation.ValidateSection("Floor", 100, (SeatingType)2)).Errors.Keys);

    [Fact]
    public void ValidateSection_RejectsBlankName() =>
        Assert.Contains("Name", Assert.Throws<VenueValidationException>(() =>
            VenueValidation.ValidateSection("  ", 100, SeatingType.GeneralAdmission)).Errors.Keys);
}

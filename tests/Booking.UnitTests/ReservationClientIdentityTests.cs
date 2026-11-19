using System.ComponentModel.DataAnnotations;
using System.Net;
using Booking.Features.BookingHolds;
using Microsoft.Extensions.Options;

namespace Booking.UnitTests;

public sealed class ReservationClientIdentityTests
{
    private static ReservationClientIdentity Identity(char key = 'a') =>
        new(Options.Create(new ReservationLimitOptions { HmacKey = new string(key, 64) }));

    [Theory]
    [InlineData("192.0.2.1", "::ffff:192.0.2.1")]
    [InlineData("2001:db8::1", "2001:0db8:0000:0000:0000:0000:0000:0001")]
    public void EquivalentAddresses_HaveSamePseudonymAcrossInstances(string first, string second)
    {
        var token = Identity().GetKey(IPAddress.Parse(first));
        Assert.Equal(token, Identity().GetKey(IPAddress.Parse(second)));
        Assert.Matches("^[0-9a-f]{64}$", token);
    }

    [Fact]
    public void OtherAddressOrSecret_HasDifferentPseudonym()
    {
        var address = IPAddress.Parse("192.0.2.1");
        Assert.NotEqual(Identity().GetKey(address), Identity().GetKey(IPAddress.Parse("192.0.2.2")));
        Assert.NotEqual(Identity().GetKey(address), Identity('b').GetKey(address));
    }

    [Fact]
    public void MissingAddress_FailsClosed() =>
        Assert.Throws<ReservationControlUnavailableException>(() => Identity().GetKey(null!));

    [Theory]
    [InlineData(0, 60, 2, 10, "valid")]
    [InlineData(10, 0, 2, 10, "valid")]
    [InlineData(10, 60, 0, 10, "valid")]
    [InlineData(10, 60, 2, 0, "valid")]
    [InlineData(10, 60, 2, 10, "")]
    [InlineData(10, 60, 2, 10, "short-key")]
    public void InvalidConfiguration_IsRejected(int attempts, int window, int active, int quantity, string secret)
    {
        var limits = new ReservationLimitOptions
        {
            AttemptsPerWindow = attempts, WindowSeconds = window,
            MaxActiveReservationsPerEvent = active, MaxReservedQuantityPerEvent = quantity,
            HmacKey = secret == "valid" ? new string('a', 64) : secret
        };
        Assert.False(Validator.TryValidateObject(limits, new ValidationContext(limits), [], validateAllProperties: true));
    }
}

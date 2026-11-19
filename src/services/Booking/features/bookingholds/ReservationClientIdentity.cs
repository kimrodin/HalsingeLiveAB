using System.Net;
using System.Security.Cryptography;
using HalsingeLive.ClientAddressing;
using Microsoft.Extensions.Options;

namespace Booking.Features.BookingHolds;

public sealed class ReservationClientIdentity(IOptions<ReservationLimitOptions> options)
{
    private readonly byte[] key = Convert.FromHexString(options.Value.HmacKey);

    public string GetKey(IPAddress address)
    {
        var normalized = ClientAddressingExtensions.NormalizeClientAddress(address)
            ?? throw new ReservationControlUnavailableException();
        return Convert.ToHexStringLower(HMACSHA256.HashData(key, normalized.GetAddressBytes()));
    }
}

public sealed class ReservationControlUnavailableException : Exception;

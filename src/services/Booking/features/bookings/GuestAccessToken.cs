using System.Security.Cryptography;
using System.Text;

namespace Booking.Features.Bookings;

public static class GuestAccessToken
{
    public static string Create()
    {
        var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return value.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static byte[] Hash(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public static bool Matches(string token, byte[] expectedHash)
    {
        if (string.IsNullOrWhiteSpace(token) || expectedHash is null || expectedHash.Length != 32)
            return false;

        return CryptographicOperations.FixedTimeEquals(Hash(token), expectedHash);
    }
}

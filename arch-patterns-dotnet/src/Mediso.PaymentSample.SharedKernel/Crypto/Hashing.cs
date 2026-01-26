using System.Security.Cryptography;
using System.Text;

namespace Mediso.PaymentSample.SharedKernel.Crypto;

public static class Hashing
{
    public static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
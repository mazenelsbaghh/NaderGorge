using System.Security.Cryptography;
using System.Text;

namespace NaderGorge.API.Configuration;

public static class ServiceTokenValidator
{
    public static bool IsValid(string? suppliedToken, params string?[] configuredTokens)
    {
        if (string.IsNullOrWhiteSpace(suppliedToken)) return false;

        foreach (var configuredToken in configuredTokens)
        {
            if (SecurityConfigurationValidator.IsUnsafeSecret(configuredToken, minLength: 32))
            {
                continue;
            }

            if (FixedTimeEquals(suppliedToken, configuredToken!))
            {
                return true;
            }
        }

        return false;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}

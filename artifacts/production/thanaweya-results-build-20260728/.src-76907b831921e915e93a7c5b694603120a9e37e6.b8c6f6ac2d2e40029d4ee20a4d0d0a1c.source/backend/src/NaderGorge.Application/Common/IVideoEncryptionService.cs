using System.Security.Cryptography;
using System.Text;

namespace NaderGorge.Application.Common;

public interface IVideoEncryptionService
{
    /// <summary>
    /// Encrypts the provider video ID along with the necessary information to create a session token.
    /// </summary>
    string EncryptVideoInfo(string providerName, string providerVideoId, string sessionKey, string? studentName = null, string? studentPhone = null);

    /// <summary>
    /// Decrypts the session token to retrieve the provider video ID (mostly for tests/validation).
    /// </summary>
    (string ProviderName, string ProviderVideoId, string? StudentName, string? StudentPhone) DecryptVideoInfo(string encryptedToken, string sessionKey);

    /// <summary>
    /// Generates a random session key suitable for AES encryption.
    /// </summary>
    string GenerateSessionKey();
}

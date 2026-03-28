using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NaderGorge.Application.Common;

namespace NaderGorge.Infrastructure.Services;

public class VideoEncryptionService : IVideoEncryptionService
{
    private const int KeySize = 32; // 256 bits
    private const int IvSize = 12; // Required for GCM
    private const int TagSize = 16; // Authentication tag size

    public string EncryptVideoInfo(string providerName, string providerVideoId, string sessionKey)
    {
        var keyBytes = Convert.FromBase64String(sessionKey);
        
        // Ensure valid key length
        if (keyBytes.Length != KeySize)
        {
            throw new ArgumentException($"Session key must be {KeySize} bytes long", nameof(sessionKey));
        }

        var payload = JsonSerializer.Serialize(new { Provider = providerName, VideoId = providerVideoId });
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var aesAlg = new AesGcm(keyBytes, TagSize);
        
        var iv = new byte[IvSize];
        RandomNumberGenerator.Fill(iv);
        
        var cipherText = new byte[payloadBytes.Length];
        var tag = new byte[TagSize];
        
        aesAlg.Encrypt(iv, payloadBytes, cipherText, tag);

        // Combine IV + CipherText + Tag into a single base64 string
        var result = new byte[iv.Length + cipherText.Length + tag.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(cipherText, 0, result, iv.Length, cipherText.Length);
        Buffer.BlockCopy(tag, 0, result, iv.Length + cipherText.Length, tag.Length);

        return Convert.ToBase64String(result);
    }

    public (string ProviderName, string ProviderVideoId) DecryptVideoInfo(string encryptedToken, string sessionKey)
    {
        var keyBytes = Convert.FromBase64String(sessionKey);
        var encryptedData = Convert.FromBase64String(encryptedToken);

        if (encryptedData.Length < IvSize + TagSize)
            throw new CryptographicException("Invalid encrypted token length");

        // Extract IV, CipherText, Tag
        var iv = new byte[IvSize];
        var tag = new byte[TagSize];
        var cipherTextLength = encryptedData.Length - IvSize - TagSize;
        var cipherText = new byte[cipherTextLength];

        Buffer.BlockCopy(encryptedData, 0, iv, 0, IvSize);
        Buffer.BlockCopy(encryptedData, IvSize, cipherText, 0, cipherTextLength);
        Buffer.BlockCopy(encryptedData, IvSize + cipherTextLength, tag, 0, TagSize);

        using var aesAlg = new AesGcm(keyBytes, TagSize);
        var plainTextBytes = new byte[cipherText.Length];
        
        aesAlg.Decrypt(iv, cipherText, tag, plainTextBytes);
        
        var plainText = Encoding.UTF8.GetString(plainTextBytes);
        var info = JsonSerializer.Deserialize<JsonElement>(plainText);
        
        return (info.GetProperty("Provider").GetString()!, info.GetProperty("VideoId").GetString()!);
    }

    public string GenerateSessionKey()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }
}

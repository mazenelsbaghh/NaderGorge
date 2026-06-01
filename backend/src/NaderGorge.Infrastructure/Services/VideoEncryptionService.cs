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

    public string EncryptVideoInfo(string providerName, string providerVideoId, string sessionKey, string? studentName = null, string? studentPhone = null)
    {
        var keyBytes = Convert.FromBase64String(sessionKey);
        
        // Ensure valid key length
        if (keyBytes.Length != KeySize)
        {
            throw new ArgumentException($"Session key must be {KeySize} bytes long", nameof(sessionKey));
        }

        var payloadObj = new Dictionary<string, string> 
        { 
            { "Provider", providerName }, 
            { "VideoId", providerVideoId } 
        };
        if (!string.IsNullOrEmpty(studentName)) payloadObj["StudentName"] = studentName;
        if (!string.IsNullOrEmpty(studentPhone)) payloadObj["StudentPhone"] = studentPhone;
        
        var payload = JsonSerializer.Serialize(payloadObj);
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

    public (string ProviderName, string ProviderVideoId, string? StudentName, string? StudentPhone) DecryptVideoInfo(string encryptedToken, string sessionKey)
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
        
        var provider = info.GetProperty("Provider").GetString()!;
        var videoId = info.GetProperty("VideoId").GetString()!;
        string? sName = info.TryGetProperty("StudentName", out var n) ? n.GetString() : null;
        string? sPhone = info.TryGetProperty("StudentPhone", out var p) ? p.GetString() : null;
        
        return (provider, videoId, sName, sPhone);
    }

    public string GenerateSessionKey()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }
}

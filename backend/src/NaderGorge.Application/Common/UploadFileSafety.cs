using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace NaderGorge.Application.Common;

public enum SafeUploadKind
{
    PublicImage,
    ProtectedResource,
    PrivateAttachment
}

public sealed record SafeUploadResult(
    string SafeFileName,
    string DisplayFileName,
    string Extension,
    string ContentType);

public static class UploadFileSafety
{
    private const int PrefixLength = 16;
    private static readonly Regex UnsafeNameCharacters = new(@"[^\p{L}\p{N}\.\-_ ]+", RegexOptions.Compiled);
    private static readonly HashSet<string> BrowserInterpretableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".svg", ".xml", ".xhtml", ".js", ".mjs", ".css"
    };

    public static SafeUploadResult Validate(
        ReadOnlySpan<byte> content,
        string fileName,
        string? declaredContentType,
        SafeUploadKind kind)
    {
        if (content.IsEmpty)
        {
            throw new InvalidUploadContentException("Uploaded file is empty.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || BrowserInterpretableExtensions.Contains(extension))
        {
            throw new InvalidUploadContentException("Uploaded file extension is not allowed.");
        }

        var detected = Detect(content);
        if (detected is null)
        {
            throw new InvalidUploadContentException("Uploaded file type could not be verified.");
        }

        if (!IsAllowed(kind, extension, detected.Value.Extension))
        {
            throw new InvalidUploadContentException("Uploaded file type does not match the allowed file policy.");
        }

        if (!string.IsNullOrWhiteSpace(declaredContentType) &&
            !IsDeclaredTypeCompatible(declaredContentType, detected.Value.ContentType))
        {
            throw new InvalidUploadContentException("Uploaded file content type does not match its bytes.");
        }

        var displayName = SanitizeDisplayName(fileName, detected.Value.Extension);
        return new SafeUploadResult(
            $"{Guid.NewGuid():N}{detected.Value.Extension}",
            displayName,
            detected.Value.Extension,
            detected.Value.ContentType);
    }

    public static SafeUploadResult Validate(byte[] content, string fileName, string? declaredContentType, SafeUploadKind kind) =>
        Validate(content.AsSpan(), fileName, declaredContentType, kind);

    public static string ComputeSha256Hex(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    private static (string Extension, string ContentType)? Detect(ReadOnlySpan<byte> content)
    {
        var prefix = content[..Math.Min(content.Length, PrefixLength)];
        if (StartsWith(prefix, [0xFF, 0xD8, 0xFF])) return (".jpg", "image/jpeg");
        if (StartsWith(prefix, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return (".png", "image/png");
        if (content.Length >= 12 &&
            content[0] == 0x52 && content[1] == 0x49 && content[2] == 0x46 && content[3] == 0x46 &&
            content[8] == 0x57 && content[9] == 0x45 && content[10] == 0x42 && content[11] == 0x50)
        {
            return (".webp", "image/webp");
        }
        if (StartsWith(prefix, [0x25, 0x50, 0x44, 0x46, 0x2D])) return (".pdf", "application/pdf");
        if (StartsWith(prefix, [0x50, 0x4B, 0x03, 0x04]) || StartsWith(prefix, [0x50, 0x4B, 0x05, 0x06]) || StartsWith(prefix, [0x50, 0x4B, 0x07, 0x08]))
        {
            return (".zip", "application/zip");
        }
        if (StartsWith(prefix, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]))
        {
            return (".doc", "application/msword");
        }
        if (StartsWith(prefix, [0x49, 0x44, 0x33]) || (content.Length > 2 && content[0] == 0xFF && (content[1] & 0xE0) == 0xE0))
        {
            return (".mp3", "audio/mpeg");
        }
        if (StartsWith(prefix, [0x4F, 0x67, 0x67, 0x53])) return (".ogg", "audio/ogg");
        if (StartsWith(prefix, [0x1A, 0x45, 0xDF, 0xA3])) return (".webm", "audio/webm");
        if (content.Length >= 12 &&
            content[4] == 0x66 && content[5] == 0x74 && content[6] == 0x79 && content[7] == 0x70)
        {
            return (".mp4", "audio/mp4");
        }
        return null;
    }

    private static bool IsAllowed(SafeUploadKind kind, string requestedExtension, string detectedExtension)
    {
        return kind switch
        {
            SafeUploadKind.PublicImage => IsImage(detectedExtension) && IsEquivalentImageExtension(requestedExtension, detectedExtension),
            SafeUploadKind.ProtectedResource => IsProtectedResourceExtension(requestedExtension, detectedExtension),
            SafeUploadKind.PrivateAttachment => IsImage(detectedExtension) || detectedExtension is ".pdf" or ".mp3" or ".mp4" or ".ogg" or ".webm",
            _ => false
        };
    }

    private static bool IsProtectedResourceExtension(string requestedExtension, string detectedExtension)
    {
        if (IsImage(detectedExtension)) return IsEquivalentImageExtension(requestedExtension, detectedExtension);
        if (detectedExtension == ".pdf") return requestedExtension == ".pdf";
        if (detectedExtension == ".doc") return requestedExtension is ".doc" or ".xls";
        if (detectedExtension == ".zip") return requestedExtension is ".zip" or ".docx" or ".xlsx";
        return false;
    }

    private static bool IsImage(string extension) => extension is ".jpg" or ".png" or ".webp";

    private static bool IsEquivalentImageExtension(string requestedExtension, string detectedExtension)
    {
        if (detectedExtension == ".jpg") return requestedExtension is ".jpg" or ".jpeg";
        return requestedExtension == detectedExtension;
    }

    private static bool IsDeclaredTypeCompatible(string declaredContentType, string detectedContentType)
    {
        var declared = declaredContentType.Split(';', 2)[0].Trim();
        if (string.Equals(declared, detectedContentType, StringComparison.OrdinalIgnoreCase)) return true;
        if (detectedContentType == "application/zip" &&
            declared is "application/zip" or "application/x-zip-compressed" or
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        {
            return true;
        }
        return false;
    }

    private static string SanitizeDisplayName(string fileName, string extension)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        baseName = UnsafeNameCharacters.Replace(baseName, "_").Trim(' ', '.', '_');
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "file";
        if (baseName.Length > 80) baseName = baseName[..80];
        return $"{baseName}{extension}";
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix) =>
        value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);
}

public sealed class InvalidUploadContentException(string message) : Exception(message);

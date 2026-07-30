using System.Security.Cryptography;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class SharedFileStorage : ISharedFileStorage
{
    private readonly IReadOnlyDictionary<SharedFileArea, string> _roots;

    public SharedFileStorage(IReadOnlyDictionary<SharedFileArea, string> roots)
    {
        _roots = roots.ToDictionary(
            pair => pair.Key,
            pair => Path.GetFullPath(pair.Value));

        foreach (var root in _roots.Values)
        {
            Directory.CreateDirectory(root);
        }
    }

    public async Task<SharedFileWriteResult> WriteAsync(
        SharedFileArea area,
        string relativePath,
        Stream content,
        CancellationToken cancellationToken)
    {
        var destination = Resolve(area, relativePath);
        var directory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("The storage path has no parent directory.");
        Directory.CreateDirectory(directory);
        EnsureNoSymbolicLink(area, directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        try
        {
            long sizeBytes = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                var buffer = new byte[128 * 1024];
                int bytesRead;
                while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    hash.AppendData(buffer, 0, bytesRead);
                    sizeBytes += bytesRead;
                }

                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, destination, overwrite: false);
            return new SharedFileWriteResult(
                NormalizeRelativePath(relativePath),
                sizeBytes,
                Convert.ToHexString(hash.GetHashAndReset()));
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(
        SharedFileArea area,
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resolved = Resolve(area, relativePath);
        EnsureNoSymbolicLink(
            area,
            Path.GetDirectoryName(resolved)
                ?? throw new InvalidOperationException("The storage path has no parent directory."));
        if (File.Exists(resolved)
            && (File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Symbolic links are not allowed inside shared storage paths.");
        }
        Stream stream = new FileStream(
            resolved,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        SharedFileArea area,
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resolved = Resolve(area, relativePath);
        EnsureNoSymbolicLink(
            area,
            Path.GetDirectoryName(resolved)
                ?? throw new InvalidOperationException("The storage path has no parent directory."));
        if (File.Exists(resolved)
            && (File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Symbolic links are not allowed inside shared storage paths.");
        }
        TryDelete(resolved);
        return Task.CompletedTask;
    }

    private string Resolve(SharedFileArea area, string relativePath)
    {
        if (!_roots.TryGetValue(area, out var configuredRoot))
        {
            throw new InvalidOperationException($"No root is configured for storage area '{area}'.");
        }

        var normalized = NormalizeRelativePath(relativePath);
        var root = configuredRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(Path.Combine(root, normalized));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!resolved.StartsWith(root, comparison))
        {
            throw new InvalidOperationException("The storage path escapes its configured root.");
        }

        return resolved;
    }

    private void EnsureNoSymbolicLink(SharedFileArea area, string destinationDirectory)
    {
        var root = _roots[area];
        var current = root;
        var relative = Path.GetRelativePath(root, destinationDirectory);
        foreach (var segment in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "Symbolic links are not allowed inside shared storage paths.");
            }
        }
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("A non-empty relative storage path is required.");
        }

        var normalized = relativePath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        if (normalized.Split(Path.DirectorySeparatorChar).Any(segment =>
                segment is "" or "." or ".."))
        {
            throw new InvalidOperationException("The storage path contains an unsafe segment.");
        }

        return normalized;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup must not hide the original write failure.
        }
    }
}

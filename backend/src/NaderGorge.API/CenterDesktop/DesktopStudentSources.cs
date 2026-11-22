using Microsoft.Extensions.Caching.Memory;

namespace NaderGorge.API.CenterDesktop;

// Student readers receive freshness metadata only, never bundle/download or program-release metadata.
public sealed record DesktopStudentSource(Guid UploadId, string CenterId, DateTimeOffset CreatedAt, DateTimeOffset ReceivedAt);

public sealed partial class CenterDesktopSupportClient
{
    public async Task<DesktopStudentSource[]> StudentSourcesAsync(CancellationToken cancellationToken)
    {
        var key = StudentCachePartition + ":sources";
        await _students.Read.WaitAsync(cancellationToken);
        try
        {
            if (_students.Snapshots.TryGetValue(key, out DesktopStudentSource[]? cached) && cached is not null)
                return cached;
            var sources = await ReadStudentSourcesAsync(cancellationToken);
            _students.Snapshots.Set(key, sources, new MemoryCacheEntryOptions
            { Size = 1L + sources.Length * 512L, AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) });
            return sources;
        }
        finally { _students.Read.Release(); }
    }

    private async Task<DesktopStudentSource[]> ReadStudentSourcesAsync(CancellationToken cancellationToken)
    {
        var latest = new Dictionary<string, DesktopUploadReceipt>(StringComparer.Ordinal);
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            if (!cursors.Add(cursor ?? "")) throw InvalidResponse();
            var page = await UploadsAsync(500, cursor, cancellationToken);
            foreach (var receipt in page.Uploads.Where(receipt => receipt.App.Role == "host"))
            {
                if (!latest.TryGetValue(receipt.CenterId, out var previous) ||
                    receipt.CreatedAt > previous.CreatedAt ||
                    receipt.CreatedAt == previous.CreatedAt && receipt.ReceivedAt > previous.ReceivedAt)
                    latest[receipt.CenterId] = receipt;
            }
            cursor = page.NextCursor.Length == 0 ? null : page.NextCursor;
        } while (cursor is not null);
        return latest.Values.OrderBy(receipt => receipt.CenterId, StringComparer.Ordinal)
            .Select(receipt => new DesktopStudentSource(receipt.UploadId, receipt.CenterId, receipt.CreatedAt, receipt.ReceivedAt))
            .ToArray();
    }
}

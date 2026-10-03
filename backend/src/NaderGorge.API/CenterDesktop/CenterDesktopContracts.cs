namespace NaderGorge.API.CenterDesktop;

public sealed record DesktopSupportStatus(bool Configured, bool Available, string Message);
public sealed record DesktopAppMetadata(string Version, string Build, string Role, string Os);
public sealed record DesktopUploadReceipt(Guid ReceiptId, Guid UploadId, string CenterId, string Sha256,
    string BundleSha256, DateTimeOffset ReceivedAt, DateTimeOffset CreatedAt, long Size, DesktopAppMetadata App);
public sealed record DesktopUploadsPage(DesktopUploadReceipt[] Uploads, string NextCursor);
public sealed record DesktopDiagnosticError(string Type, int? Code);
public sealed record DesktopDiagnosticFrame(string File, int Frame, int Line, int Column);
// Deliberately no message, path, credentials, arbitrary extension data, or database fields.
public sealed record DesktopDiagnosticEvent(int Schema, string Kind, Guid Id, Guid Session,
    DateTimeOffset Time, string Version, string Platform, string Operation, string? Build, string? Role,
    DesktopDiagnosticError[]? Errors, DesktopDiagnosticFrame[]? Frames);
public sealed record DesktopDiagnostics(DesktopUploadReceipt Receipt, string Kind,
    DesktopDiagnosticEvent[] Events, int Total, bool Truncated);
public sealed record DesktopReleaseManifest(string ReleaseId, string Version, string Build,
    string Platform, string Role, long Size, string Sha256, string DownloadPath, string? Notes);
public sealed record DesktopReleaseSlot(string Platform, string Role, string Status, DesktopReleaseManifest? Manifest);
public sealed record DesktopReleases(DesktopReleaseSlot[] Releases);

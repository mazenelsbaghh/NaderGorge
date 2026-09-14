namespace NaderGorge.Application.Common;

/// <summary>
/// Indicates whether the submitted artifact was applied or is already retained by the database.
/// </summary>
public sealed record AiCallbackReceipt(bool Accepted);

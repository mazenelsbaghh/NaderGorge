namespace NaderGorge.Domain.Enums;

/// <summary>
/// Describes whether a Bunny asset is the media source currently attached to a
/// lesson video, an in-flight replacement, or retained history.
/// </summary>
public enum BunnyVideoAssetSourceState
{
    Current = 0,
    PendingReplacement = 1,
    Retired = 2
}

namespace NaderGorge.Application.Features.Admin.Commands;

internal enum BunnyVideoLifecycleState
{
    Processing,
    Ready,
    Failed,
    Unknown
}

internal static class BunnyVideoStatusClassifier
{
    public static BunnyVideoLifecycleState Classify(int bunnyStatus) => bunnyStatus switch
    {
        // Bunny Stream video status values: Created, Uploaded, Processing,
        // Transcoding, Finished, Error, UploadFailed, JitSegmenting and
        // JitPlaylistsCreated. Webhook-only values must never make a video
        // playable before the video API reports Finished.
        0 or 1 or 2 or 3 or 7 or 8 => BunnyVideoLifecycleState.Processing,
        4 => BunnyVideoLifecycleState.Ready,
        5 or 6 => BunnyVideoLifecycleState.Failed,
        _ => BunnyVideoLifecycleState.Unknown
    };
}

namespace NaderGorge.Application.Interfaces;

public interface IAiJobCancellationStore
{
    Task RequestVideoAnalysisCancellationAsync(Guid videoId);
    Task RequestMindmapCancellationAsync(Guid videoId);
    Task ClearVideoAnalysisCancellationAsync(Guid videoId);
    Task ClearMindmapCancellationAsync(Guid videoId);
}

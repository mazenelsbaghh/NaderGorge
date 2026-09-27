import { fetchPlaybackInternalResponse, rejectPlaybackResponse } from './video-playback-session';
import { resolveYouTubeHlsSource, type YouTubeHlsSource } from './youtube-hls-source';

export async function resolveSessionYouTubeHlsSource(request: Request, context: {
  sessionId: string; videoId: string; authorization: string; surface?: string; version?: string;
}): Promise<YouTubeHlsSource> {
  return resolveYouTubeHlsSource(context.videoId, context.version, {
    async read(_videoId, version) {
      const response = await fetchPlaybackInternalResponse(request, context.sessionId, {
        ...context, resource: 'youtube-hls-source', query: version ? new URLSearchParams({ v: version }) : undefined,
      });
      if (response.status === 404) { await response.body?.cancel(); return undefined; }
      if (!response.ok) await rejectPlaybackResponse(response);
      return response.json() as Promise<YouTubeHlsSource>;
    },
    async write(source) {
      const response = await fetchPlaybackInternalResponse(request, context.sessionId, {
        ...context, resource: 'youtube-hls-source', body: JSON.stringify(source),
      });
      if (!response.ok) await rejectPlaybackResponse(response);
      await response.body?.cancel();
    },
  });
}

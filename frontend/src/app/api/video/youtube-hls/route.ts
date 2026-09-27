import { decryptVideoEmbedMaterial } from '@/lib/video-embed-material';
import { validateVideoMediaRequest } from '@/lib/video-embed-request-guard';
import { fetchPlaybackMaterial, isPlaybackSessionId, playbackErrorResponse, PlaybackRequestError, readPlaybackSession } from '@/lib/video-playback-session';
import { getYouTubeHlsPlaylist, YouTubeHlsError } from '@/lib/youtube-hls-source';
import { resolveSessionYouTubeHlsSource } from '@/lib/youtube-hls-session-source';

const privateHeaders = { 'Cache-Control': 'no-store, private', 'X-Content-Type-Options': 'nosniff' };

export async function GET(request: Request): Promise<Response> {
  try {
    if (validateVideoMediaRequest(request.url, request.headers)) throw new PlaybackRequestError(403);
    const params = new URL(request.url).searchParams;
    const sessionId = params.get('s') ?? '';
    if (!isPlaybackSessionId(sessionId)) throw new PlaybackRequestError(400);
    const browserSession = readPlaybackSession(request, sessionId);
    const material = await fetchPlaybackMaterial(request, sessionId, browserSession);
    const video = decryptVideoEmbedMaterial(material);
    if (video.Provider?.toLowerCase() !== 'youtube-hls') throw new PlaybackRequestError(403);
    const source = await resolveSessionYouTubeHlsSource(request, { ...browserSession, sessionId,
      videoId: video.VideoId, version: params.get('v') ?? undefined });
    if (params.get('info') === '1') {
      return Response.json({ qualities: source.qualities, durationSeconds: source.durationSeconds,
        expiresAt: source.expiresAt, version: source.version, serverNowMs: Date.now() }, { headers: privateHeaders });
    }
    const playlist = getYouTubeHlsPlaylist(source, {
      sessionId, playlist: params.get('playlist') ?? 'master', quality: params.get('quality') ?? undefined,
    });
    return new Response(playlist, { headers: { ...privateHeaders, 'Content-Type': 'application/vnd.apple.mpegurl' } });
  } catch (error) {
    if (error instanceof YouTubeHlsError) return playbackErrorResponse(new PlaybackRequestError(error.status));
    return playbackErrorResponse(error);
  }
}

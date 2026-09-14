import { bunnyHlsResource, bunnyHlsRoot, relayBunnyResource } from '@/lib/bunny-hls-relay';
import { decryptVideoEmbedMaterial } from '@/lib/video-embed-material';
import { validateVideoMediaRequest } from '@/lib/video-embed-request-guard';
import { fetchPlaybackMaterial, playbackErrorResponse, readPlaybackSession } from '@/lib/video-playback-session';

export async function GET(request: Request): Promise<Response> {
  if (validateVideoMediaRequest(request.url, request.headers)) return new Response(null, { status: 403 });
  const params = new URL(request.url).searchParams;
  const sessionId = params.get('s') ?? '';
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(sessionId)) {
    return new Response(null, { status: 400 });
  }
  try {
    const browserSession = readPlaybackSession(request, sessionId);
    const material = await fetchPlaybackMaterial(request, sessionId, browserSession);
    const video = decryptVideoEmbedMaterial(material);
    if (video.Provider?.toLowerCase() !== 'bunny-hls') return new Response(null, { status: 403 });
    const root = bunnyHlsRoot(video.VideoId);
    const upstream = bunnyHlsResource(root, params.get('path') ?? 'playlist.m3u8');
    return await relayBunnyResource(upstream, sessionId, root, request);
  } catch (error) {
    return playbackErrorResponse(error);
  }
}

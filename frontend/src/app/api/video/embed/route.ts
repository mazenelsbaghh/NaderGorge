import { validateVideoEmbedNavigation } from '@/lib/video-embed-request-guard';
import { PlaybackRequestError, readPlaybackBootstrap } from '@/lib/video-playback-session';
import { videoBootstrapHtml, videoPlayerResponse } from '@/lib/video-player-response';

export async function GET(request: Request) {
  try {
    if (validateVideoEmbedNavigation(request.url, request.headers)) throw new PlaybackRequestError(403);
    const sessionId = new URL(request.url).searchParams.get('s') ?? '';
    readPlaybackBootstrap(request, sessionId);
    return videoPlayerResponse(videoBootstrapHtml(sessionId));
  } catch (error) {
    const status = error instanceof PlaybackRequestError ? error.status : 502;
    return videoPlayerResponse(`<!DOCTYPE html><html><body style="margin:0;background:#000"><script>
window.parent.postMessage({source:'video-embed',type:'bootstrapError',data:{status:${status}}},window.location.origin);
</script></body></html>`, status);
  }
}

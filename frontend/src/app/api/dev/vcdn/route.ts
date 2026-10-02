import { generateVcdnEmbedHtml } from '@/lib/vcdn-embed';
import { mintVcdnPlaybackSource } from '@/lib/vcdn-playback-source';
import { videoPlayerResponse } from '@/lib/video-player-response';

export async function GET(request: Request) {
  const url = new URL(request.url);
  if (process.env.NODE_ENV !== 'development' || !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)
    || !/^(localhost|127\.0\.0\.1|\[::1\])(?::\d+)?$/i.test(request.headers.get('host') ?? '')) return new Response(null, { status: 404 });
  const videoId = process.env.VCDN_PREVIEW_VIDEO_ID;
  if (!videoId) return new Response(null, { status: 404 });
  const grant = await mintVcdnPlaybackSource(request, videoId, Date.now() + 600_000);
  if (url.searchParams.get('info') === '1') return Response.json(grant, { headers: { 'Cache-Control': 'no-store' } });
  return videoPlayerResponse(generateVcdnEmbedHtml(videoId, 'تجربة محلية', '', grant));
}

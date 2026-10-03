import { generateVideoEmbedHtml } from '@/lib/video-embed-html';

export function GET(request: Request) {
  const hostname = new URL(request.url).hostname;
  if (process.env.NODE_ENV !== 'development' || !['localhost', '127.0.0.1', '[::1]'].includes(hostname)) {
    return new Response(null, { status: 404 });
  }
  const appearance = JSON.parse(process.env.MASSAR_LOCAL_PLAYER_APPEARANCE || '{}');
  if (new URL(request.url).searchParams.get('settings') === '1') {
    return Response.json(appearance, { headers: { 'Cache-Control': 'no-store' } });
  }
  const videoId = process.env.MASSAR_LOCAL_YOUTUBE_PREVIEW_VIDEO_ID || 'aqz-KE-bpKQ';
  if (!/^[A-Za-z0-9_-]{11}$/.test(videoId)) return new Response(null, { status: 500 });
  return new Response(generateVideoEmbedHtml('youtube', videoId, {
    studentName: 'تجربة محلية',
    youtubeQualityEnabled: true,
    youtubeQualityBottomCoverPercent: Number(appearance.YouTubeQualityBottomCoverPercent ?? 0),
    youtubeQualityMobileBottomCoverPercent: Number(appearance.YouTubeQualityMobileBottomCoverPercent ?? 0),
  }), {
    headers: {
      'Content-Type': 'text/html; charset=utf-8',
      'Cache-Control': 'no-store',
      'X-Frame-Options': 'SAMEORIGIN',
      'Content-Security-Policy': "frame-ancestors 'self'",
      'Referrer-Policy': 'strict-origin-when-cross-origin',
    },
  });
}

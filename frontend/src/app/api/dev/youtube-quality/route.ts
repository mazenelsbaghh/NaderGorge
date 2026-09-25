import { generateVideoEmbedHtml } from '@/lib/video-embed-html';

export function GET(request: Request) {
  const hostname = new URL(request.url).hostname;
  if (process.env.NODE_ENV !== 'development' || !['localhost', '127.0.0.1', '[::1]'].includes(hostname)) {
    return new Response(null, { status: 404 });
  }
  return new Response(generateVideoEmbedHtml('youtube', 'aqz-KE-bpKQ', {
    studentName: 'تجربة محلية',
    youtubeQualityEnabled: true,
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

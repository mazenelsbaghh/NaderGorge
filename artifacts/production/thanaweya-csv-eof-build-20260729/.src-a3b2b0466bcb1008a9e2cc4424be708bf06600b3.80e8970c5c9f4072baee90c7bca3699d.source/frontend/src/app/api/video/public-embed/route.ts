import { NextRequest, NextResponse } from 'next/server';
import { generateVideoEmbedHtml } from '../embed/route';

function errorPage(message: string, status = 400) {
  return new NextResponse(`<!DOCTYPE html><html lang="ar" dir="rtl"><body style="margin:0;background:#000;color:#fff;font-family:system-ui,sans-serif;display:grid;place-items:center;height:100vh">${message}</body></html>`, {
    status,
    headers: {
      'Content-Type': 'text/html; charset=utf-8',
      'Cache-Control': 'no-store',
      'X-Frame-Options': 'SAMEORIGIN',
      'Content-Security-Policy': "frame-ancestors 'self'",
      'Referrer-Policy': 'strict-origin-when-cross-origin',
    },
  });
}

function parseVideoUrl(value: string) {
  let url: URL;
  try { url = new URL(value); } catch { return null; }
  if (!['http:', 'https:'].includes(url.protocol)) return null;
  const host = url.hostname.toLowerCase().replace(/^www\./, '');

  if (host === 'youtu.be' || host.endsWith('youtube.com')) {
    const id = host === 'youtu.be'
      ? url.pathname.slice(1)
      : url.searchParams.get('v') || url.pathname.match(/\/embed\/([^/?]+)/)?.[1] || url.pathname.match(/\/shorts\/([^/?]+)/)?.[1];
    return id ? { provider: 'youtube', id } : null;
  }
  if (host === 'vk.com' || host.endsWith('.vk.com')) {
    const oid = url.searchParams.get('oid');
    const id = url.searchParams.get('id');
    return oid && id ? { provider: 'vk', id: `oid=${oid}&id=${id}` } : null;
  }
  if (host === 'player.mediadelivery.net' || host.endsWith('.bunnycdn.com')) {
    const parts = url.pathname.split('/').filter(Boolean);
    const id = parts.at(-1);
    return id ? { provider: 'bunny', id } : null;
  }
  return null;
}

export async function GET(request: NextRequest) {
  const host = request.headers.get('host');
  const referer = request.headers.get('referer');
  if (referer && host && !referer.includes(host)) return errorPage('Unauthorized embedding', 403);

  const source = request.nextUrl.searchParams.get('url');
  if (!source) return errorPage('Missing video URL');
  const parsed = parseVideoUrl(source);
  if (!parsed) return errorPage('رابط الفيديو غير مدعوم. استخدم رابط YouTube أو VK أو Bunny صحيح.');

  const html = generateVideoEmbedHtml(parsed.provider, parsed.id, 'Massar Academy', '');
  return new NextResponse(html, {
    headers: {
      'Content-Type': 'text/html; charset=utf-8',
      'Cache-Control': 'no-store',
      'X-Content-Type-Options': 'nosniff',
      'X-Frame-Options': 'SAMEORIGIN',
      'Content-Security-Policy': "frame-ancestors 'self'",
      // The generated page creates a cross-origin YouTube iframe.
      'Referrer-Policy': 'strict-origin-when-cross-origin',
    },
  });
}

import { NextRequest, NextResponse } from 'next/server';

const ALLOWED_IMAGE_CONTENT_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp', 'image/gif', 'image/svg+xml']);

function getAllowedHosts() {
  const hosts = new Set(['assets.massar-academy.net']);
  const apiUrl = process.env.NEXT_PUBLIC_API_URL;

  if (apiUrl) {
    try {
      hosts.add(new URL(apiUrl).host);
    } catch {
      // Ignore invalid configuration here. The request validator still blocks unknown hosts.
    }
  }

  return hosts;
}

export async function GET(request: NextRequest) {
  const rawUrl = request.nextUrl.searchParams.get('url');
  if (!rawUrl) {
    return new NextResponse('Missing media url', { status: 400 });
  }

  let mediaUrl: URL;
  try {
    mediaUrl = new URL(rawUrl);
  } catch {
    return new NextResponse('Invalid media url', { status: 400 });
  }

  if (!['http:', 'https:'].includes(mediaUrl.protocol) || !getAllowedHosts().has(mediaUrl.host)) {
    return new NextResponse('Media host is not allowed', { status: 400 });
  }

  const upstream = await fetch(mediaUrl, {
    cache: 'no-store',
    headers: { Accept: 'image/*' },
  });

  if (!upstream.ok) {
    return new NextResponse('Unable to fetch media', { status: upstream.status });
  }

  const contentType = upstream.headers.get('content-type')?.split(';')[0]?.toLowerCase() || '';
  if (!ALLOWED_IMAGE_CONTENT_TYPES.has(contentType)) {
    return new NextResponse('Unsupported media type', { status: 415 });
  }

  const body = await upstream.arrayBuffer();
  return new NextResponse(body, {
    headers: {
      'Cache-Control': 'public, max-age=300',
      'Content-Type': contentType,
    },
  });
}

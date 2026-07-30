import { NextRequest, NextResponse } from 'next/server';

export function GET(request: NextRequest) {
  const provider = request.nextUrl.searchParams.get('provider');
  const videoId = request.nextUrl.searchParams.get('id')?.trim();

  if (!videoId || !/^[A-Za-z0-9_-]+$/.test(videoId)) {
    return new NextResponse('Invalid video identifier', { status: 400 });
  }

  if (provider === 'youtube') {
    return NextResponse.redirect(`https://www.youtube-nocookie.com/embed/${videoId}`);
  }

  if (provider === 'bunny') {
    const libraryId = process.env.BUNNY_STREAM_LIBRARY_ID || process.env.NEXT_PUBLIC_BUNNY_STREAM_LIBRARY_ID;
    if (!libraryId) return new NextResponse('Bunny player is not configured', { status: 503 });
    return NextResponse.redirect(`https://player.mediadelivery.net/embed/${libraryId}/${videoId}`);
  }

  return new NextResponse('Unsupported video provider', { status: 400 });
}

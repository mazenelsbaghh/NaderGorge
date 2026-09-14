import { NextRequest, NextResponse } from 'next/server';
import { isBunnyLibraryId, isBunnyVideoGuid } from '@/lib/bunny-video-reference';

export function GET(request: NextRequest) {
  const provider = request.nextUrl.searchParams.get('provider');
  const videoId = request.nextUrl.searchParams.get('id')?.trim();

  if (provider === 'youtube') {
    if (!videoId || !/^[A-Za-z0-9_-]+$/.test(videoId)) {
      return new NextResponse('Invalid video identifier', { status: 400 });
    }
    return NextResponse.redirect(`https://www.youtube-nocookie.com/embed/${videoId}`);
  }

  if (provider === 'bunny') {
    if (!videoId || !isBunnyVideoGuid(videoId)) {
      return new NextResponse('Invalid Bunny video identifier', { status: 400 });
    }

    const requestedLibraryId = request.nextUrl.searchParams.get('libraryId')?.trim();
    const legacyLibraryId = (process.env.BUNNY_STREAM_LIBRARY_ID || process.env.NEXT_PUBLIC_BUNNY_STREAM_LIBRARY_ID || '').trim();
    const libraryId = requestedLibraryId || legacyLibraryId;
    if (!isBunnyLibraryId(libraryId)) {
      return new NextResponse('A valid Bunny libraryId is required', { status: 400 });
    }
    return NextResponse.redirect(`https://player.mediadelivery.net/embed/${libraryId}/${videoId}`);
  }

  return new NextResponse('Unsupported video provider', { status: 400 });
}

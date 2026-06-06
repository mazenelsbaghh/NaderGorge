import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

import {
  getRouteBoundaryDecision,
  getSurfaceName,
  getSurfaceOrigins,
} from '@/packages/surface-runtime/config';

function withSurfaceHeader(response: NextResponse, surface: string) {
  response.headers.set('x-masar-surface', surface);
  return response;
}

export function proxy(request: NextRequest) {
  const url = request.nextUrl.clone();
  const hostname = request.headers.get('host') || '';
  const surface = getSurfaceName();
  const surfaceDecision = getRouteBoundaryDecision({
    surface,
    pathname: url.pathname,
    search: url.search,
    host: hostname,
  });

  if (surfaceDecision.action === 'rewrite' && surfaceDecision.destination) {
    url.pathname = surfaceDecision.destination;
    return withSurfaceHeader(NextResponse.rewrite(url), surfaceDecision.surface);
  }

  if (surfaceDecision.action === 'redirect' && surfaceDecision.destination) {
    return withSurfaceHeader(NextResponse.redirect(surfaceDecision.destination), surfaceDecision.surface);
  }

  if (surface !== 'all') {
    return withSurfaceHeader(NextResponse.next(), surface);
  }

  const { mainDomain } = getSurfaceOrigins();
  const isAdminSubdomain = hostname.startsWith('admin.');

  if (isAdminSubdomain) {
    if (url.pathname === '/') {
      url.pathname = '/admin';
      return withSurfaceHeader(NextResponse.rewrite(url), 'admin');
    }

    if (url.pathname.startsWith('/student')) {
      const proto = request.headers.get('x-forwarded-proto') || 'https';
      return withSurfaceHeader(
        NextResponse.redirect(new URL(`${proto}://${mainDomain}/student`, request.url)),
        'admin',
      );
    }
  } else if (url.pathname.startsWith('/admin')) {
    const isLocal = hostname.includes('localhost') || hostname.includes('127.0.0.1');
    if (!isLocal) {
      url.pathname = '/_not-found';
      return withSurfaceHeader(NextResponse.rewrite(url), 'landing');
    }
  }

  return withSurfaceHeader(NextResponse.next(), 'all');
}

export const config = {
  matcher: [
    '/((?!api|_next/static|_next/image|favicon.ico|images).*)',
  ],
};

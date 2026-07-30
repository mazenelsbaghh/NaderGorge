import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

import {
  getRouteBoundaryDecision,
} from '@/packages/surface-runtime/config';
import type { SurfaceName } from '@/packages/surface-runtime/config';

function withSurfaceHeader(response: NextResponse, surface: string) {
  response.headers.set('x-massar-surface', surface);
  return response;
}

function detectSurfaceFromRequest(request: NextRequest): SurfaceName {
  const envSurface = process.env.APP_SURFACE || process.env.NEXT_PUBLIC_APP_SURFACE;
  if (envSurface === 'landing' || envSurface === 'student' || envSurface === 'admin' || envSurface === 'teacher' || envSurface === 'assistant') {
    return envSurface;
  }

  const hostname = request.headers.get('host') || '';
  const isLocal = hostname.includes('localhost') || hostname.includes('127.0.0.1');

  if (isLocal) {
    const port = request.nextUrl.port || new URL(request.url).port;
    if (port === '8738') return 'landing';
    if (port === '8739') return 'student';
    if (port === '8740') return 'admin';
    if (port === '8741') return 'teacher';
    if (port === '8742') return 'assistant';
  }

  if (hostname.startsWith('admin.') || hostname.startsWith('super.')) return 'admin';
  if (hostname.startsWith('app.') || hostname.startsWith('student.')) return 'student';
  if (hostname.startsWith('teacher.')) return 'teacher';
  if (hostname.startsWith('staff.') || hostname.startsWith('assistant.')) return 'assistant';

  return 'landing';
}

export function proxy(request: NextRequest) {
  const url = request.nextUrl.clone();
  const hostname = request.headers.get('host') || '';
  const surface = detectSurfaceFromRequest(request);

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
    return withSurfaceHeader(NextResponse.redirect(new URL(surfaceDecision.destination, request.url)), surfaceDecision.surface);
  }

  // Handle subdomain root rewrites (e.g. "/" on app.massar-academy.net goes to "/student")
  if (surface === 'admin' && url.pathname === '/') {
    url.pathname = '/admin';
    return withSurfaceHeader(NextResponse.rewrite(url), 'admin');
  }
  if (surface === 'student' && url.pathname === '/') {
    url.pathname = '/student';
    return withSurfaceHeader(NextResponse.rewrite(url), 'student');
  }
  if (surface === 'teacher' && url.pathname === '/') {
    url.pathname = '/teacher';
    return withSurfaceHeader(NextResponse.rewrite(url), 'teacher');
  }
  if (surface === 'assistant' && url.pathname === '/') {
    url.pathname = '/assistant';
    return withSurfaceHeader(NextResponse.rewrite(url), 'assistant');
  }

  return withSurfaceHeader(NextResponse.next(), surface);
}

export const config = {
  matcher: [
    '/((?!api|_next/static|_next/image|favicon.ico|images).*)',
  ],
};

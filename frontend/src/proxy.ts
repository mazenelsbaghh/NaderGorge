import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

import {
  getRouteBoundaryDecision,
  getSurfaceName,
  getSurfaceOrigins,
} from '@/packages/surface-runtime/config';

function withSurfaceHeader(response: NextResponse, surface: string) {
  response.headers.set('x-massar-surface', surface);
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
  const isAdminSubdomain = hostname.startsWith('admin.') || hostname.startsWith('super.');
  const isStudentSubdomain = hostname.startsWith('app.') || hostname.startsWith('student.');
  const isTeacherSubdomain = hostname.startsWith('teacher.');
  const isAssistantSubdomain = hostname.startsWith('staff.') || hostname.startsWith('assistant.');

  const proto = request.headers.get('x-forwarded-proto') || 'https';

  if (isAdminSubdomain) {
    if (url.pathname === '/') {
      url.pathname = '/admin';
      return withSurfaceHeader(NextResponse.rewrite(url), 'admin');
    }

    if (url.pathname.startsWith('/student') || url.pathname.startsWith('/teacher') || url.pathname.startsWith('/assistant')) {
      return withSurfaceHeader(
        NextResponse.redirect(new URL(`${proto}://${mainDomain}${url.pathname}`, request.url)),
        'admin',
      );
    }
  } else if (isStudentSubdomain) {
    if (url.pathname === '/') {
      url.pathname = '/student';
      return withSurfaceHeader(NextResponse.rewrite(url), 'student');
    }

    if (url.pathname.startsWith('/admin') || url.pathname.startsWith('/teacher') || url.pathname.startsWith('/assistant')) {
      return withSurfaceHeader(
        NextResponse.redirect(new URL(`${proto}://${mainDomain}${url.pathname}`, request.url)),
        'student',
      );
    }
  } else if (isTeacherSubdomain) {
    if (url.pathname === '/') {
      url.pathname = '/teacher';
      return withSurfaceHeader(NextResponse.rewrite(url), 'teacher');
    }

    if (url.pathname.startsWith('/admin') || url.pathname.startsWith('/student') || url.pathname.startsWith('/assistant')) {
      return withSurfaceHeader(
        NextResponse.redirect(new URL(`${proto}://${mainDomain}${url.pathname}`, request.url)),
        'teacher',
      );
    }
  } else if (isAssistantSubdomain) {
    if (url.pathname === '/') {
      url.pathname = '/assistant';
      return withSurfaceHeader(NextResponse.rewrite(url), 'assistant');
    }

    if (url.pathname.startsWith('/admin') || url.pathname.startsWith('/student') || url.pathname.startsWith('/teacher')) {
      return withSurfaceHeader(
        NextResponse.redirect(new URL(`${proto}://${mainDomain}${url.pathname}`, request.url)),
        'assistant',
      );
    }
  } else {
    // If accessing landing domain with administrative/student paths, redirect to proper subdomains
    if (url.pathname.startsWith('/admin')) {
      return withSurfaceHeader(
        NextResponse.redirect(new URL(`${proto}://admin.${mainDomain}/admin`, request.url)),
        'landing'
      );
    }
    if (url.pathname.startsWith('/student')) {
      return withSurfaceHeader(
        NextResponse.redirect(new URL(`${proto}://app.${mainDomain}/student`, request.url)),
        'landing'
      );
    }
    if (url.pathname.startsWith('/teacher')) {
      return withSurfaceHeader(
        NextResponse.redirect(new URL(`${proto}://teacher.${mainDomain}/teacher`, request.url)),
        'landing'
      );
    }
    if (url.pathname.startsWith('/assistant')) {
      return withSurfaceHeader(
        NextResponse.redirect(new URL(`${proto}://staff.${mainDomain}/assistant`, request.url)),
        'landing'
      );
    }
  }

  return withSurfaceHeader(NextResponse.next(), 'all');
}

export const config = {
  matcher: [
    '/((?!api|_next/static|_next/image|favicon.ico|images).*)',
  ],
};

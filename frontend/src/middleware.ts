import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  const url = request.nextUrl.clone();
  const hostname = request.headers.get('host') || '';

  // Detect if accessing the admin subdomain
  const isAdminSubdomain = hostname.startsWith('admin.');

  // 1. If accessing admin.bsma-academy.com
  if (isAdminSubdomain) {
    // If requesting the root "/" on admin subdomain, rewrite it to "/admin"
    if (url.pathname === '/') {
      url.pathname = '/admin';
      return NextResponse.rewrite(url);
    }
    
    // If requesting student area on admin subdomain, redirect to main domain
    if (url.pathname.startsWith('/student')) {
      const proto = request.headers.get('x-forwarded-proto') || 'https';
      return NextResponse.redirect(new URL(`${proto}://bsma-academy.com/student`, request.url));
    }
  } else {
    // 2. If accessing main domain bsma-academy.com
    // IF TRYING TO ACCESS "/admin" ON THE MAIN DOMAIN: SHOW 404 NOT FOUND (COMPLETELY HIDE THE ADMIN ROUTES)
    if (url.pathname.startsWith('/admin')) {
      const isLocal = hostname.includes('localhost') || hostname.includes('127.0.0.1');
      if (!isLocal) {
        url.pathname = '/_not-found';
        return NextResponse.rewrite(url);
      }
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    /*
     * Match all request paths except for the ones starting with:
     * - api (API routes)
     * - _next/static (static files)
     * - _next/image (image optimization files)
     * - favicon.ico (favicon file)
     * - images (public images)
     */
    '/((?!api|_next/static|_next/image|favicon.ico|images).*)',
  ],
};

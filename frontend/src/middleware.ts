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
    // If trying to access "/admin" on the main domain, redirect to the admin subdomain
    if (url.pathname.startsWith('/admin')) {
      const proto = request.headers.get('x-forwarded-proto') || 'https';
      
      // Preserve hostname for local dev, redirect to admin. subdomain on production
      const isLocal = hostname.includes('localhost') || hostname.includes('127.0.0.1');
      const targetHost = isLocal ? hostname : `admin.${hostname}`;

      if (targetHost !== hostname) {
        return NextResponse.redirect(new URL(`${proto}://${targetHost}${url.pathname}${url.search}`, request.url));
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

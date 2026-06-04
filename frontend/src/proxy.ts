import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

const mainDomain = process.env.NEXT_PUBLIC_APP_DOMAIN || 'nadergeorge.academy';

export function proxy(request: NextRequest) {
  const url = request.nextUrl.clone();
  const hostname = request.headers.get('host') || '';
  const isAdminSubdomain = hostname.startsWith('admin.');

  if (isAdminSubdomain) {
    if (url.pathname === '/') {
      url.pathname = '/admin';
      return NextResponse.rewrite(url);
    }

    if (url.pathname.startsWith('/student')) {
      const proto = request.headers.get('x-forwarded-proto') || 'https';
      return NextResponse.redirect(new URL(`${proto}://${mainDomain}/student`, request.url));
    }
  } else if (url.pathname.startsWith('/admin')) {
    const isLocal = hostname.includes('localhost') || hostname.includes('127.0.0.1');
    if (!isLocal) {
      url.pathname = '/_not-found';
      return NextResponse.rewrite(url);
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    '/((?!api|_next/static|_next/image|favicon.ico|images).*)',
  ],
};

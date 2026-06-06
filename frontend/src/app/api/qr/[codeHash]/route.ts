import { NextRequest, NextResponse } from 'next/server';

/**
 * QR compatibility route — GET /api/qr/[codeHash]
 *
 * The current auth model stores JWTs in browser storage, so this server route
 * cannot redeem directly. It redirects to a client page that can use the active
 * student session.
 */

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ codeHash: string }> }
) {
  const { codeHash } = await params;

  // Resolve base URL dynamically to handle reverse proxy domains correctly
  const host = request.headers.get('x-forwarded-host') || request.headers.get('host') || process.env.NEXT_PUBLIC_APP_DOMAIN || 'masaracademy.com';
  const proto = request.headers.get('x-forwarded-proto') || 'https';
  const baseUrl = process.env.NEXT_PUBLIC_APP_URL || `${proto}://${host}`;

  return NextResponse.redirect(new URL(`/qr/${encodeURIComponent(codeHash)}`, baseUrl));
}

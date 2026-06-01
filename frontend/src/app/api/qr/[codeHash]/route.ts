import { NextRequest, NextResponse } from 'next/server';

/**
 * QR Auto-Redeem Route — GET /api/qr/[codeHash]
 *
 * When a student scans a QR code:
 * 1. If authenticated → auto-redeem the code and redirect to content
 * 2. If not authenticated → redirect to login with returnUrl
 *
 * The QR URL format is: {baseUrl}/api/qr/{codeHash}
 */

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ codeHash: string }> }
) {
  const { codeHash } = await params;
  let backendUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
  if (backendUrl.includes('localhost:5245')) {
    backendUrl = backendUrl.replace('localhost:5245', 'backend:5245');
  }


  // Check if user has an auth token
  const authToken = request.cookies.get('token')?.value;

  if (!authToken) {
    // Not authenticated: redirect to login with return URL
    const returnUrl = encodeURIComponent(`/api/qr/${codeHash}`);
    return NextResponse.redirect(new URL(`/login?returnUrl=${returnUrl}`, request.url));
  }

  try {
    // Auto-redeem: call the activate endpoint
    const response = await fetch(`${backendUrl}/codes/activate`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${authToken}`,
      },
      body: JSON.stringify({ code: codeHash }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      const errorMsg = errorData?.message || 'فشل تفعيل الكود';
      return NextResponse.redirect(
        new URL(`/student?error=${encodeURIComponent(errorMsg)}`, request.url)
      );
    }

    const data = await response.json();
    const redirectUrl = data?.data?.redirectUrl || '/student';

    return NextResponse.redirect(new URL(redirectUrl, request.url));
  } catch {
    return NextResponse.redirect(
      new URL('/student?error=حدث خطأ أثناء تفعيل الكود', request.url)
    );
  }
}

import { NextRequest, NextResponse } from 'next/server';
import { getSurfaceOrigins } from '@/packages/surface-runtime/config';

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ codeHash: string }> }
) {
  const { codeHash } = await params;
  const origins = getSurfaceOrigins();
  const studentOrigin = origins.student;

  return NextResponse.redirect(new URL(`/qr/${encodeURIComponent(codeHash)}`, studentOrigin));
}

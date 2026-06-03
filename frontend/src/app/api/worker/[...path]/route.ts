import { NextRequest, NextResponse } from 'next/server';

/**
 * Next.js API proxy for the worker service (BullMQ status API).
 * 
 * The worker runs on port 3001 inside Docker (container: nadergorge_worker).
 * In development, it's accessible at localhost:3001.
 * In production (Docker), it's accessible at worker:3001.
 * 
 * This proxy allows the browser to call /api/worker/status/... instead of
 * hardcoding localhost:3001, which doesn't work in production.
 */

const WORKER_URL = process.env.WORKER_URL || 'http://worker:3001';

async function proxyToWorker(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  const workerPath = path.join('/');
  const targetUrl = `${WORKER_URL}/api/${workerPath}`;

  try {
    const fetchOptions: RequestInit = {
      method: request.method,
      headers: {
        'Content-Type': 'application/json',
      },
    };

    // Forward body for POST/PUT/PATCH
    if (['POST', 'PUT', 'PATCH'].includes(request.method)) {
      try {
        const body = await request.text();
        if (body) fetchOptions.body = body;
      } catch {
        // No body
      }
    }

    const response = await fetch(targetUrl, fetchOptions);
    
    const contentType = response.headers.get('content-type') || '';
    
    if (contentType.includes('application/json')) {
      const data = await response.json();
      return NextResponse.json(data, { status: response.status });
    }
    
    const text = await response.text();
    return new NextResponse(text, {
      status: response.status,
      headers: { 'Content-Type': contentType },
    });
  } catch (error) {
    console.error(`[worker-proxy] Failed to reach worker at ${targetUrl}:`, error);
    return NextResponse.json(
      { error: 'Worker service unavailable' },
      { status: 503 }
    );
  }
}

export async function GET(request: NextRequest, context: { params: Promise<{ path: string[] }> }) {
  return proxyToWorker(request, context);
}

export async function POST(request: NextRequest, context: { params: Promise<{ path: string[] }> }) {
  return proxyToWorker(request, context);
}

export async function DELETE(request: NextRequest, context: { params: Promise<{ path: string[] }> }) {
  return proxyToWorker(request, context);
}

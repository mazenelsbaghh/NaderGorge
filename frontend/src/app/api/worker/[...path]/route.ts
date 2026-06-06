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
const WORKER_ADMIN_TOKEN = process.env.WORKER_ADMIN_TOKEN;
const API_URL = (process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://backend:5245/api').replace(/\/$/, '');
const STAFF_ROLES = new Set(['Admin', 'Teacher']);

function isAllowedWorkerRoute(method: string, path: string[]) {
  if (path.length === 2 && path[0] === 'status' && method === 'GET') return true;
  if (path.length === 2 && path[0] === 'status' && method === 'DELETE') return true;
  if (path.length === 3 && path[0] === 'status' && path[2] === 'retry' && method === 'POST') return true;
  return false;
}

type CurrentUserResponse = {
  roles?: string[];
  data?: {
    roles?: string[];
  };
};

async function validateStaffAuthorization(authorization: string | null) {
  if (!authorization?.startsWith('Bearer ')) {
    return { ok: false as const, status: 401, error: 'Authentication required' };
  }

  try {
    const response = await fetch(`${API_URL}/auth/me`, {
      headers: { Authorization: authorization },
      cache: 'no-store',
    });

    if (!response.ok) {
      return { ok: false as const, status: 401, error: 'Authentication required' };
    }

    const user = (await response.json()) as CurrentUserResponse;
    const roles = user.roles ?? user.data?.roles ?? [];
    const isStaff = roles.some((role) => STAFF_ROLES.has(role));

    if (!isStaff) {
      return { ok: false as const, status: 403, error: 'Staff role required' };
    }

    return { ok: true as const };
  } catch (error) {
    console.error('[worker-proxy] Failed to validate staff authorization:', error);
    return { ok: false as const, status: 503, error: 'Authentication service unavailable' };
  }
}

async function proxyToWorker(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  if (!isAllowedWorkerRoute(request.method, path)) {
    return NextResponse.json({ error: 'Worker route is not allowed' }, { status: 404 });
  }

  const staffAuth = await validateStaffAuthorization(request.headers.get('authorization'));
  if (!staffAuth.ok) {
    return NextResponse.json({ error: staffAuth.error }, { status: staffAuth.status });
  }

  if (!WORKER_ADMIN_TOKEN) {
    return NextResponse.json({ error: 'Worker proxy is not configured' }, { status: 503 });
  }

  const workerPath = path.join('/');
  const targetUrl = `${WORKER_URL}/api/${workerPath}`;

  try {
    const fetchOptions: RequestInit = {
      method: request.method,
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${WORKER_ADMIN_TOKEN}`,
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

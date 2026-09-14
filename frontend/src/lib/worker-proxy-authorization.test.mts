import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';
import ts from 'typescript';
import * as jobStatus from './ai-job-status.ts';

type Route = (request: Request, context: { params: Promise<{ path: string[] }> }) => Promise<Response>;
async function workerRoutes(fetch: typeof globalThis.fetch) {
  const source = await readFile(new URL('../app/api/worker/[...path]/route.ts', import.meta.url), 'utf8');
  const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText;
  const exports: Partial<Record<'GET' | 'POST' | 'DELETE', Route>> = {};
  vm.runInNewContext(compiled, {
    exports, require: (name: string) => name === 'next/server' ? { NextResponse: Response } : jobStatus,
    fetch, console,
    process: { env: { INTERNAL_API_URL: 'https://backend.test/api', WORKER_URL: 'https://worker.test', WORKER_ADMIN_TOKEN: 'worker-secret' } },
  });
  return exports as Record<'GET' | 'POST' | 'DELETE', Route>;
}

for (const actor of [
  { name: 'content staff', roles: ['Staff'], permissions: ['content.manage'], allowed: true },
  { name: 'custom staff role', roles: ['Content editor'], permissions: ['CONTENT.MANAGE'], allowed: true },
  { name: 'unrelated staff', roles: ['Staff'], permissions: ['users.manage'], allowed: false },
  { name: 'student', roles: ['Student'], permissions: [], allowed: false },
  { name: 'admin', roles: ['Admin'], permissions: [], allowed: true },
  { name: 'teacher', roles: ['Teacher'], permissions: [], allowed: true },
]) {
  for (const method of ['GET', 'POST', 'DELETE'] as const) {
    test(`${actor.name}: worker ${method} enforces authenticated content access`, async () => {
      let workerRequests = 0;
      const routes = await workerRoutes(async (url, options) => {
        if (String(url) === 'https://backend.test/api/auth/session') {
          assert.equal(new Headers(options?.headers).get('authorization'), 'Bearer staff-token');
          return Response.json({ success: true, data: { user: actor } });
        }
        assert.ok(String(url).startsWith('https://worker.test/api/status/'));
        workerRequests++;
        assert.equal(new Headers(options?.headers).get('authorization'), 'Bearer worker-secret');
        return Response.json(method === 'GET' ? { state: 'active', progress: 45 } : { success: true });
      });
      const path = method === 'POST' ? ['status', 'video-id', 'retry'] : ['status', 'video-id'];
      const response = await routes[method](new Request('https://staff.massar-academy.net/api/worker/' + path.join('/'), {
        method, headers: { authorization: 'Bearer staff-token' },
      }), { params: Promise.resolve({ path }) });
      assert.equal(response.status, actor.allowed ? 200 : 403);
      assert.equal(workerRequests, actor.allowed ? 1 : 0);
      if (actor.allowed && method === 'GET') assert.equal((await response.json()).state, 'active');
    });
  }
}

test('expired staff session never reaches the worker', async () => {
  const routes = await workerRoutes(async url => {
    assert.equal(String(url), 'https://backend.test/api/auth/session');
    return new Response(null, { status: 401 });
  });
  const response = await routes.GET(new Request('https://staff.massar-academy.net/api/worker/status/video-id', {
    headers: { authorization: 'Bearer expired' },
  }), { params: Promise.resolve({ path: ['status', 'video-id'] }) });
  assert.equal(response.status, 401);
});

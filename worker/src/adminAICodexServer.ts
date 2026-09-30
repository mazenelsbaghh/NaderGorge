import http from 'node:http';
import { chmod, lstat, unlink } from 'node:fs/promises';
import type { AdminAIProviderRequest } from './services/adminAIAgent.js';
import { runAdminAICodex } from './services/adminAICodexRuntime.js';

const MAX_BODY_BYTES = 192 * 1024;

function validRequest(value: unknown): value is AdminAIProviderRequest {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const request = value as Record<string, unknown>;
  return typeof request.model === 'string' && typeof request.systemInstruction === 'string' &&
    typeof request.deadlineAt === 'string' && Number.isFinite(Date.parse(request.deadlineAt)) &&
    Array.isArray(request.contents) && Array.isArray(request.readFunctions) && request.readFunctions.length <= 128;
}

export function createAdminAICodexServer(run: typeof runAdminAICodex = runAdminAICodex): http.Server {
  return http.createServer(async (incoming, response) => {
    if (incoming.method === 'GET' && incoming.url === '/health') {
      response.writeHead(200, { 'content-type': 'application/json' }); response.end('{"status":"ready"}'); return;
    }
    if (incoming.method !== 'POST' || incoming.url !== '/infer' || incoming.headers['content-type'] !== 'application/json') {
      response.writeHead(404); response.end(); return;
    }
    const chunks: Buffer[] = [];
    let size = 0;
    try {
      for await (const chunk of incoming) {
        const buffer = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk);
        size += buffer.length;
        if (size > MAX_BODY_BYTES) throw new Error('REQUEST_TOO_LARGE');
        chunks.push(buffer);
      }
      const request: unknown = JSON.parse(Buffer.concat(chunks).toString('utf8'));
      if (!validRequest(request)) throw new Error('INVALID_REQUEST');
      const result = await run(request);
      response.writeHead(200, { 'content-type': 'application/json' }); response.end(JSON.stringify(result));
    } catch {
      if (!response.headersSent) response.writeHead(422, { 'content-type': 'application/json' });
      response.end('{"error":"AI_PROVIDER_FAILURE"}');
    }
  });
}

async function main(): Promise<void> {
  const socketPath = process.env.ADMIN_AI_CODEX_SOCKET;
  if (!socketPath?.startsWith('/')) throw new Error('ADMIN_AI_CODEX_SOCKET is required');
  try {
    const existing = await lstat(socketPath);
    if (!existing.isSocket()) throw new Error('Codex socket path is not a socket');
    await unlink(socketPath);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== 'ENOENT') throw error;
  }
  const server = createAdminAICodexServer();
  await new Promise<void>((resolve, reject) => { server.once('error', reject); server.listen(socketPath, resolve); });
  await chmod(socketPath, 0o660);
  const stop = () => server.close(() => process.exit(0));
  process.once('SIGINT', stop); process.once('SIGTERM', stop);
}

if (process.argv[1]?.endsWith('/adminAICodexServer.js')) {
  main().catch(() => { process.exitCode = 1; });
}

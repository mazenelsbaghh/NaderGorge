import http from 'node:http';
import type { AdminAIProviderRequest, AdminAIProviderResponse } from './adminAIAgent.js';

const MAX_REQUEST_BYTES = 192 * 1024;
const MAX_RESPONSE_BYTES = 256 * 1024;

export async function checkAdminAICodexHealth(): Promise<boolean> {
  const socketPath = process.env.ADMIN_AI_CODEX_SOCKET;
  if (!socketPath?.startsWith('/')) return false;
  return new Promise(resolve => {
    const outgoing = http.get({ socketPath, path: '/health' }, response => {
      response.resume();
      resolve(response.statusCode === 200);
    });
    outgoing.setTimeout(2_000, () => outgoing.destroy());
    outgoing.on('error', () => resolve(false));
  });
}

export async function requestAdminAICodex(request: AdminAIProviderRequest): Promise<AdminAIProviderResponse> {
  const socketPath = process.env.ADMIN_AI_CODEX_SOCKET;
  if (!socketPath?.startsWith('/')) throw new Error('AI_PROVIDER_UNAVAILABLE');
  const body = JSON.stringify(request);
  if (Buffer.byteLength(body, 'utf8') > MAX_REQUEST_BYTES) throw new Error('REDACTED_CONTEXT_LIMIT');
  const remainingMs = Date.parse(request.deadlineAt) - Date.now();
  if (!Number.isFinite(remainingMs) || remainingMs <= 0) throw new Error('AI_PROVIDER_TIMEOUT');

  return new Promise<AdminAIProviderResponse>((resolve, reject) => {
    const outgoing = http.request({ socketPath, path: '/infer', method: 'POST', headers: {
      'content-type': 'application/json', 'content-length': Buffer.byteLength(body, 'utf8'),
    } }, response => {
      const chunks: Buffer[] = [];
      let size = 0;
      response.on('data', (chunk: Buffer) => {
        size += chunk.length;
        if (size > MAX_RESPONSE_BYTES) { outgoing.destroy(new Error('AI_PROVIDER_FAILURE')); return; }
        chunks.push(chunk);
      });
      response.on('end', () => {
        if (response.statusCode !== 200) { reject(new Error('AI_PROVIDER_FAILURE')); return; }
        try {
          const parsed = JSON.parse(Buffer.concat(chunks).toString('utf8')) as AdminAIProviderResponse;
          if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('AI_PROVIDER_FAILURE');
          resolve(parsed);
        } catch { reject(new Error('AI_PROVIDER_FAILURE')); }
      });
    });
    outgoing.setTimeout(Math.min(remainingMs, 120_000), () => outgoing.destroy(new Error('AI_PROVIDER_TIMEOUT')));
    outgoing.on('error', error => reject(error.message === 'AI_PROVIDER_TIMEOUT' ? error : new Error('AI_PROVIDER_FAILURE')));
    outgoing.end(body);
  });
}

import assert from 'node:assert/strict';
import { mkdtemp, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { createAdminAICodexServer } from '../adminAICodexServer.js';
import { requestAdminAICodex } from './adminAICodexProvider.js';
import type { AdminAIProviderRequest } from './adminAIAgent.js';

test('worker reaches only the local Codex socket and receives a bounded decision', async () => {
  const folder = await mkdtemp(path.join(os.tmpdir(), 'admin-ai-codex-test-'));
  const socket = path.join(folder, 'codex.sock');
  const previous = process.env.ADMIN_AI_CODEX_SOCKET;
  const server = createAdminAICodexServer(async request => {
    assert.equal(request.model, 'gpt-5.6-sol');
    return { text: '{"schemaVersion":"1","type":"refuse","refusal":{"reasonCode":"OUT_OF_SCOPE","messageAr":"لا"}}', responseId: 'thread-1' };
  });
  try {
    await new Promise<void>((resolve, reject) => { server.once('error', reject); server.listen(socket, resolve); });
    process.env.ADMIN_AI_CODEX_SOCKET = socket;
    const request: AdminAIProviderRequest = {
      model: 'gpt-5.6-sol', systemInstruction: 'test', contents: [], readFunctions: [],
      deadlineAt: new Date(Date.now() + 10_000).toISOString(),
    };
    const result = await requestAdminAICodex(request);
    assert.equal(result.responseId, 'thread-1');
    assert.match(result.text || '', /OUT_OF_SCOPE/);
  } finally {
    if (previous === undefined) delete process.env.ADMIN_AI_CODEX_SOCKET; else process.env.ADMIN_AI_CODEX_SOCKET = previous;
    await new Promise<void>(resolve => server.close(() => resolve()));
    await rm(folder, { recursive: true, force: true });
  }
});

import assert from 'node:assert/strict';
import { chmod, mkdtemp, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { parseCodexEvents, runAdminAICodex } from './adminAICodexRuntime.js';
import type { AdminAIProviderRequest } from './adminAIAgent.js';

const request: AdminAIProviderRequest = {
  model: 'gpt-5.6-sol', systemInstruction: 'Return a decision', contents: [],
  readFunctions: [{ name: 'read_0', description: 'Search', parametersJsonSchema: { type: 'object' } }],
  deadlineAt: new Date(Date.now() + 60_000).toISOString(),
};

function events(text: string, itemType = 'agent_message') {
  return [
    JSON.stringify({ type: 'thread.started', thread_id: 'thread-1' }),
    JSON.stringify({ type: 'turn.started' }),
    JSON.stringify({ type: 'item.completed', item: { id: 'item-1', type: itemType, text } }),
    JSON.stringify({ type: 'turn.completed', usage: { input_tokens: 9, output_tokens: 8 } }),
  ].join('\n');
}

test('Codex terminal output carries only the final decision and usage', () => {
  const decision = { schemaVersion: '1', type: 'refuse', refusal: { reasonCode: 'OUT_OF_SCOPE', messageAr: 'خارج النطاق' } };
  assert.deepEqual(parseCodexEvents(events(JSON.stringify(decision)), request), {
    text: JSON.stringify(decision), responseId: 'thread-1', inputTokenCount: 9, outputTokenCount: 8,
  });
});

test('Codex read requests become only named bounded backend read calls', () => {
  const output = { schemaVersion: '1', type: 'request_reads', calls: [{ id: 'call-1', name: 'read_0', args: { query: 'نادر' } }] };
  assert.deepEqual(parseCodexEvents(events(JSON.stringify(output)), request).functionCalls, [
    { id: 'call-1', name: 'read_0', args: { query: 'نادر' } },
  ]);
  assert.throws(() => parseCodexEvents(events(JSON.stringify({ ...output, calls: [{ ...output.calls[0], name: 'read_9' }] })), request), /AI_INVALID_DECISION/);
});

test('Codex runtime rejects tool activity and incomplete turns', () => {
  assert.throws(() => parseCodexEvents(events('{}', 'command_execution'), request), /AI_PROVIDER_TOOL_FORBIDDEN/);
  assert.throws(() => parseCodexEvents(JSON.stringify({ type: 'item.completed', item: { type: 'agent_message', text: '{}' } }), request), /AI_PROVIDER_FAILURE/);
});

test('Codex process receives no worker secrets and cannot use shell or web tools', async () => {
  const folder = await mkdtemp(path.join(os.tmpdir(), 'admin-ai-codex-process-'));
  const binary = path.join(folder, 'fake-codex');
  const previous = {
    binary: process.env.ADMIN_AI_CODEX_BINARY,
    home: process.env.CODEX_HOME,
    callback: process.env.AI_CALLBACK_SECRET,
  };
  try {
    await writeFile(binary, `#!${process.execPath}
const args = process.argv.slice(2);
process.stdin.resume();
process.stdin.on('end', () => {
  const result = { hasCallbackSecret: Boolean(process.env.AI_CALLBACK_SECRET), args };
  process.stdout.write(JSON.stringify({ type: 'thread.started', thread_id: 'fake-thread' }) + '\\n');
  process.stdout.write(JSON.stringify({ type: 'item.completed', item: { type: 'agent_message', text: JSON.stringify(result) } }) + '\\n');
  process.stdout.write(JSON.stringify({ type: 'turn.completed', usage: { input_tokens: 1, output_tokens: 2 } }) + '\\n');
});
`);
    await chmod(binary, 0o700);
    process.env.ADMIN_AI_CODEX_BINARY = binary;
    process.env.CODEX_HOME = folder;
    process.env.AI_CALLBACK_SECRET = 'test-secret-must-not-leak';
    const result = await runAdminAICodex(request);
    const observed = JSON.parse(result.text || '{}') as { hasCallbackSecret: boolean; args: string[] };
    assert.equal(observed.hasCallbackSecret, false);
    assert.ok(observed.args.includes('read-only'));
    assert.ok(observed.args.includes('shell_tool'));
    assert.ok(observed.args.includes('unified_exec'));
    assert.ok(observed.args.includes('plugins'));
    assert.ok(observed.args.includes('web_search="disabled"'));
  } finally {
    for (const [key, value] of Object.entries({ ADMIN_AI_CODEX_BINARY: previous.binary, CODEX_HOME: previous.home, AI_CALLBACK_SECRET: previous.callback })) {
      if (value === undefined) delete process.env[key]; else process.env[key] = value;
    }
    await rm(folder, { recursive: true, force: true });
  }
});

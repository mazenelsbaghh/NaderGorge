import { spawn } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import type { AdminAIProviderRequest, AdminAIProviderResponse } from './adminAIAgent.js';

const MAX_EVENT_BYTES = 1024 * 1024;
const MAX_PROMPT_BYTES = 192 * 1024;
const MODEL_PATTERN = /^[a-z0-9][a-z0-9._-]{0,79}$/;
const ALLOWED_ITEM_TYPES = new Set(['agent_message', 'reasoning']);

interface CodexEvent { type?: string; thread_id?: string; item?: { type?: string; text?: string }; usage?: { input_tokens?: number; output_tokens?: number } }

function parseReadCalls(decision: Record<string, unknown>, request: AdminAIProviderRequest) {
  if (decision.schemaVersion !== '1' || Object.keys(decision).some(key => !['schemaVersion', 'type', 'calls'].includes(key))) throw new Error('AI_INVALID_DECISION');
  if (!Array.isArray(decision.calls) || decision.calls.length < 1 || decision.calls.length > 4) throw new Error('AI_INVALID_DECISION');
  const allowedNames = new Set(request.readFunctions.map(tool => tool.name));
  const callIds = new Set<string>();
  return decision.calls.map((candidate: unknown) => {
    if (!candidate || typeof candidate !== 'object' || Array.isArray(candidate)) throw new Error('AI_INVALID_DECISION');
    const call = candidate as Record<string, unknown>;
    if (Object.keys(call).length !== 3 || Object.keys(call).some(key => !['id', 'name', 'args'].includes(key)) ||
        typeof call.name !== 'string' || !allowedNames.has(call.name) ||
        typeof call.id !== 'string' || call.id.length < 1 || call.id.length > 160 || callIds.has(call.id) ||
        !call.args || typeof call.args !== 'object' || Array.isArray(call.args)) throw new Error('AI_INVALID_DECISION');
    callIds.add(call.id);
    return { id: call.id, name: call.name, args: call.args };
  });
}

export function parseCodexEvents(output: string, request: AdminAIProviderRequest): AdminAIProviderResponse {
  let finalText: string | undefined;
  let responseId: string | null = null;
  let inputTokenCount: number | null = null;
  let outputTokenCount: number | null = null;
  let completed = false;
  for (const line of output.split('\n').filter(Boolean)) {
    const event = JSON.parse(line) as CodexEvent;
    if (event.type === 'thread.started') responseId = typeof event.thread_id === 'string' ? event.thread_id : null;
    if (event.type === 'error' || event.type === 'turn.failed') throw new Error('AI_PROVIDER_FAILURE');
    if (event.type?.startsWith('item.')) {
      const itemType = event.item?.type;
      if (!itemType || !ALLOWED_ITEM_TYPES.has(itemType)) throw new Error('AI_PROVIDER_TOOL_FORBIDDEN');
      if (event.type === 'item.completed' && itemType === 'agent_message' && typeof event.item?.text === 'string') finalText = event.item.text;
    }
    if (event.type === 'turn.completed') {
      completed = true;
      inputTokenCount = Number.isSafeInteger(event.usage?.input_tokens) ? event.usage!.input_tokens! : null;
      outputTokenCount = Number.isSafeInteger(event.usage?.output_tokens) ? event.usage!.output_tokens! : null;
    }
  }
  if (!completed || !finalText) throw new Error('AI_PROVIDER_FAILURE');
  let parsedDecision: unknown;
  try { parsedDecision = JSON.parse(finalText); } catch { return { text: finalText, responseId, inputTokenCount, outputTokenCount }; }
  if (parsedDecision && typeof parsedDecision === 'object' && !Array.isArray(parsedDecision) && (parsedDecision as Record<string, unknown>).type === 'request_reads') {
    return { functionCalls: parseReadCalls(parsedDecision as Record<string, unknown>, request), responseId, inputTokenCount, outputTokenCount };
  }
  return { text: finalText, responseId, inputTokenCount, outputTokenCount };
}

function codexPrompt(request: AdminAIProviderRequest): string {
  const prompt = JSON.stringify({
    instructions: request.systemInstruction,
    conversation: request.contents,
    availableReads: request.readFunctions,
    requiredOutput: 'Return one JSON object only. For a read, use {"schemaVersion":"1","type":"request_reads","calls":[{"id":"unique-id","name":"read_0","args":{}}]}. For a terminal decision, use the exact decision contract in instructions. Never run tools.',
  });
  if (Buffer.byteLength(prompt, 'utf8') > MAX_PROMPT_BYTES) throw new Error('REDACTED_CONTEXT_LIMIT');
  return prompt;
}

function codexArguments(model: string): string[] {
  const args = ['exec', '--ephemeral', '--skip-git-repo-check', '--sandbox', 'read-only', '--ignore-user-config', '--ignore-rules'];
  for (const feature of ['shell_tool', 'unified_exec', 'apps', 'browser_use', 'computer_use', 'multi_agent', 'hooks', 'plugins', 'remote_plugin', 'image_generation', 'view_image']) args.push('--disable', feature);
  args.push('-c', 'web_search="disabled"', '--model', model, '--json', '-');
  return args;
}

function codexEnvironment(codexHome: string, cwd: string): NodeJS.ProcessEnv {
  return {
    CODEX_HOME: codexHome, HOME: cwd, PATH: `${path.dirname(process.execPath)}:/app/node_modules/.bin:/usr/local/bin:/usr/bin:/bin`, LANG: 'C.UTF-8',
    HTTPS_PROXY: process.env.HTTPS_PROXY, HTTP_PROXY: process.env.HTTP_PROXY, NO_PROXY: process.env.NO_PROXY,
  };
}

function invokeCodex(binary: string, args: string[], prompt: string, cwd: string, environment: NodeJS.ProcessEnv, timeoutMs: number): Promise<string> {
  return new Promise((resolve, reject) => {
    const child = spawn(binary, args, { cwd, env: environment, stdio: ['pipe', 'pipe', 'ignore'] });
    let output = '';
    let failed = false;
    const fail = (reason: string) => { if (failed) return; failed = true; child.kill('SIGKILL'); reject(new Error(reason)); };
    const timer = setTimeout(() => fail('AI_PROVIDER_TIMEOUT'), timeoutMs);
    child.stdout.setEncoding('utf8');
    child.stdout.on('data', (chunk: string) => {
      output += chunk;
      if (Buffer.byteLength(output, 'utf8') > MAX_EVENT_BYTES) fail('AI_PROVIDER_FAILURE');
    });
    child.stdin.on('error', () => fail('AI_PROVIDER_FAILURE'));
    child.on('error', () => fail('AI_PROVIDER_FAILURE'));
    child.on('close', code => { clearTimeout(timer); if (!failed) code === 0 ? resolve(output) : reject(new Error('AI_PROVIDER_FAILURE')); });
    child.stdin.end(prompt);
  });
}

export async function runAdminAICodex(request: AdminAIProviderRequest): Promise<AdminAIProviderResponse> {
  if (!MODEL_PATTERN.test(request.model) || !Array.isArray(request.contents) || !Array.isArray(request.readFunctions)) throw new Error('AI_INVALID_REQUEST');
  const codexHome = process.env.CODEX_HOME;
  if (!codexHome?.startsWith('/')) throw new Error('AI_PROVIDER_UNAVAILABLE');
  const prompt = codexPrompt(request);
  const remainingMs = Date.parse(request.deadlineAt) - Date.now();
  if (!Number.isFinite(remainingMs) || remainingMs <= 0) throw new Error('AI_PROVIDER_TIMEOUT');
  const cwd = await mkdtemp(path.join(os.tmpdir(), 'admin-ai-codex-'));
  try {
    const binary = process.env.ADMIN_AI_CODEX_BINARY || '/app/node_modules/.bin/codex';
    const output = await invokeCodex(binary, codexArguments(request.model), prompt, cwd, codexEnvironment(codexHome, cwd), Math.min(remainingMs, 120_000));
    return parseCodexEvents(output, request);
  } finally { await rm(cwd, { recursive: true, force: true }); }
}

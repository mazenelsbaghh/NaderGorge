import { isUnsafeSecret } from '../security.js';
import type { LiveSupportClaimContext } from './liveSupportAgent.js';

const MAX_RESPONSE_BYTES = 128 * 1024;
const DEFAULT_TIMEOUT_MS = 10_000;

export type LiveSupportCallbackErrorCode =
  | 'CALLBACK_TIMEOUT'
  | 'CALLBACK_UNAVAILABLE'
  | 'CALLBACK_REJECTED'
  | 'CALLBACK_RESPONSE_TOO_LARGE'
  | 'CALLBACK_INVALID_RESPONSE';

export class LiveSupportCallbackError extends Error {
  constructor(public readonly code: LiveSupportCallbackErrorCode, public readonly retryable: boolean) {
    super(code);
    this.name = 'LiveSupportCallbackError';
  }
}

export interface LiveSupportCompletionPayload {
  schemaVersion: '1';
  expectedConversationVersion: number;
  expectedPolicyVersionId: string;
  decision: unknown;
  decisionHash: string;
  callbackIdempotencyKey: string;
  provider: string;
  model: string;
  providerResponseId: string | null;
  inputTokenCount: number | null;
  outputTokenCount: number | null;
  latencyMs: number;
}

export interface LiveSupportFailurePayload {
  failureCode: string;
  callbackIdempotencyKey: string;
  provider: string | null;
  model: string | null;
  latencyMs: number;
}

export interface LiveSupportCallbackClient {
  claim(turnId: string): Promise<LiveSupportClaimContext | null>;
  complete(turnId: string, payload: LiveSupportCompletionPayload): Promise<string>;
  fail(turnId: string, payload: LiveSupportFailurePayload): Promise<string>;
}

interface ClientOptions {
  baseUrl?: string;
  token?: string;
  timeoutMs?: number;
  fetchImpl?: typeof fetch;
}

async function boundedBody(response: Response): Promise<string> {
  const declared = Number(response.headers.get('content-length'));
  if (Number.isFinite(declared) && declared > MAX_RESPONSE_BYTES) {
    throw new LiveSupportCallbackError('CALLBACK_RESPONSE_TOO_LARGE', false);
  }
  const bytes = new Uint8Array(await response.arrayBuffer());
  if (bytes.byteLength > MAX_RESPONSE_BYTES) throw new LiveSupportCallbackError('CALLBACK_RESPONSE_TOO_LARGE', false);
  return new TextDecoder().decode(bytes);
}

export function createLiveSupportCallbackClient(options: ClientOptions = {}): LiveSupportCallbackClient {
  const token = options.token ?? process.env.AI_CALLBACK_SECRET;
  if (isUnsafeSecret(token, 32)) throw new Error('AI_CALLBACK_SECRET is missing, weak, or unsafe.');
  const rawBase = options.baseUrl ?? process.env.BACKEND_API_URL ?? 'http://localhost:5245';
  const baseUrl = `${rawBase.replace(/\/$/, '').replace(/\/api\/v1$/, '')}/api/v1`;
  const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
  const fetchImpl = options.fetchImpl ?? fetch;

  async function request(path: string, init: RequestInit): Promise<{ status: number; body: string }> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    try {
      const response = await fetchImpl(`${baseUrl}${path}`, {
        ...init,
        signal: controller.signal,
        headers: { 'Content-Type': 'application/json', 'X-Internal-Token': token!, ...init.headers },
      });
      const body = await boundedBody(response);
      if (!response.ok) {
        if (response.status === 404 && path.endsWith('/claim')) return { status: response.status, body };
        const retryable = response.status === 408 || response.status === 429 || response.status >= 500;
        throw new LiveSupportCallbackError('CALLBACK_REJECTED', retryable);
      }
      return { status: response.status, body };
    } catch (error) {
      if (error instanceof LiveSupportCallbackError) throw error;
      if (controller.signal.aborted) throw new LiveSupportCallbackError('CALLBACK_TIMEOUT', true);
      throw new LiveSupportCallbackError('CALLBACK_UNAVAILABLE', true);
    } finally {
      clearTimeout(timer);
    }
  }

  return {
    async claim(turnId) {
      const response = await request(`/internal/callbacks/live-support-ai/turns/${encodeURIComponent(turnId)}/claim`, { method: 'POST' });
      if (response.status === 404) return null;
      try {
        return JSON.parse(response.body) as LiveSupportClaimContext;
      } catch {
        throw new LiveSupportCallbackError('CALLBACK_INVALID_RESPONSE', false);
      }
    },
    async complete(turnId, payload) {
      const response = await request(`/internal/callbacks/live-support-ai/turns/${encodeURIComponent(turnId)}/complete`, { method: 'POST', body: JSON.stringify(payload) });
      return response.body;
    },
    async fail(turnId, payload) {
      const response = await request(`/internal/callbacks/live-support-ai/turns/${encodeURIComponent(turnId)}/fail`, { method: 'POST', body: JSON.stringify(payload) });
      return response.body;
    },
  };
}

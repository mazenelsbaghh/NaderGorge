import { isUnsafeSecret } from '../security.js';
import type { LiveSupportClaimContext } from './liveSupportAgent.js';
import { fetchWithTimeout, WorkerExternalError } from './workerFetch.js';

const MAX_RESPONSE_BYTES = 128 * 1024;
const DEFAULT_TIMEOUT_MS = 10_000;

function isNonEmptyString(value: unknown, maximum: number): value is string {
  return typeof value === 'string' && value.length > 0 && value.length <= maximum;
}

function isUuid(value: unknown): value is string {
  return typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

export function validateLiveSupportClaimContext(value: unknown, expectedTurnId: string): LiveSupportClaimContext {
  if (!value || typeof value !== 'object') throw new LiveSupportCallbackError('CALLBACK_INVALID_RESPONSE', false);
  const context = value as Partial<LiveSupportClaimContext>;
  if (context.schemaVersion !== '1' || context.turnId !== expectedTurnId || !isUuid(context.turnId) || !isUuid(context.conversationId) || !isUuid(context.policyVersionId)) {
    throw new LiveSupportCallbackError('CALLBACK_INVALID_RESPONSE', false);
  }
  const conversationVersion = context.expectedConversationVersion;
  if (typeof conversationVersion !== 'number' || !Number.isSafeInteger(conversationVersion) || conversationVersion < 0 || !isNonEmptyString(context.callbackIdempotencyKey, 100)) {
    throw new LiveSupportCallbackError('CALLBACK_INVALID_RESPONSE', false);
  }
  if (!isNonEmptyString(context.deadlineAt, 100) || !Number.isFinite(Date.parse(context.deadlineAt)) || Date.parse(context.deadlineAt) <= Date.now()) {
    throw new LiveSupportCallbackError('CALLBACK_INVALID_RESPONSE', false);
  }
  if (!isNonEmptyString(context.systemInstructions, 52_000) || !Array.isArray(context.knowledgeDocuments) || context.studentContext === null || Array.isArray(context.studentContext) || typeof context.studentContext !== 'object' || !Array.isArray(context.messages) || !Array.isArray(context.allowedActions) || !Array.isArray(context.allowedDecisionTypes)) {
    throw new LiveSupportCallbackError('CALLBACK_INVALID_RESPONSE', false);
  }
  if (context.knowledgeDocuments.length > 50 || context.messages.length > 100 || context.allowedActions.length > 100 || context.allowedDecisionTypes.length > 20) {
    throw new LiveSupportCallbackError('CALLBACK_INVALID_RESPONSE', false);
  }
  return context as LiveSupportClaimContext;
}

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
  const fetchImpl = options.fetchImpl;

  async function request(path: string, init: RequestInit): Promise<{ status: number; body: string }> {
    const url = `${baseUrl}${path}`;
    const headers = { 'Content-Type': 'application/json', 'X-Internal-Token': token!, ...init.headers };
    let controller: AbortController | null = null;
    let timer: NodeJS.Timeout | null = null;
    try {
      let response: Response;
      if (fetchImpl) {
        controller = new AbortController();
        timer = setTimeout(() => controller?.abort(), timeoutMs);
        response = await fetchImpl(url, { ...init, signal: controller.signal, headers });
      } else {
        response = await fetchWithTimeout(url, { ...init, headers, timeoutMs, maxResponseBytes: MAX_RESPONSE_BYTES });
      }
      const body = await boundedBody(response);
      if (!response.ok) {
        if (response.status === 404 && path.endsWith('/claim')) return { status: response.status, body };
        const retryable = response.status === 408 || response.status === 429 || response.status >= 500;
        throw new LiveSupportCallbackError('CALLBACK_REJECTED', retryable);
      }
      return { status: response.status, body };
    } catch (error) {
      if (error instanceof LiveSupportCallbackError) throw error;
      if (error instanceof WorkerExternalError && error.category === 'response-too-large') {
        throw new LiveSupportCallbackError('CALLBACK_RESPONSE_TOO_LARGE', false);
      }
      if ((error instanceof WorkerExternalError && error.category === 'timeout') || controller?.signal.aborted) {
        throw new LiveSupportCallbackError('CALLBACK_TIMEOUT', true);
      }
      throw new LiveSupportCallbackError('CALLBACK_UNAVAILABLE', true);
    } finally {
      if (timer) clearTimeout(timer);
    }
  }

  return {
    async claim(turnId) {
      const response = await request(`/internal/callbacks/live-support-ai/turns/${encodeURIComponent(turnId)}/claim`, { method: 'POST' });
      if (response.status === 404) return null;
      try {
        return validateLiveSupportClaimContext(JSON.parse(response.body), turnId);
      } catch (error) {
        if (error instanceof LiveSupportCallbackError) throw error;
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

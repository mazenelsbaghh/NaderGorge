import { isUnsafeSecret } from '../security.js';
import { fetchWithTimeout, WorkerExternalError } from './workerFetch.js';

const MAX_RESPONSE_BYTES = 128 * 1024;
type JsonObject = Record<string, unknown>;

export type AdminAICallbackErrorCode = 'CALLBACK_TIMEOUT' | 'CALLBACK_UNAVAILABLE' | 'CALLBACK_REJECTED' | 'CALLBACK_RESPONSE_TOO_LARGE' | 'CALLBACK_INVALID_RESPONSE';
export class AdminAICallbackError extends Error {
  constructor(public readonly code: AdminAICallbackErrorCode, public readonly retryable: boolean, public readonly httpStatus?: number) {
    super(httpStatus ? `${code}_HTTP_${httpStatus}` : code); this.name = 'AdminAICallbackError';
  }
}

export interface AdminAIClaimContext extends JsonObject {
  schemaVersion: '1'; turnId: string; conversationId: string; actorAdminUserId: string;
  stepNumber: number; expectedTurnVersion: number; expectedConversationVersion: number; expectedSecurityVersion: number;
  leaseToken: string; leaseExpiresAt: string; callbackIdempotencyKey: string; deadlineAt: string;
  systemInstructions: string; messages: unknown[]; readTools: unknown[]; actionTools: unknown[]; budgets: JsonObject;
  capabilityBaseline: { id: string; version: string; manifestHash: string };
  sensitiveDataPolicy: { id: string; version: string; policyHash: string };
}
export interface AdminAICallbackClient {
  claim(turnId: string, workerInstanceId: string): Promise<AdminAIClaimContext | null>;
  renew(turnId: string, payload: JsonObject): Promise<JsonObject>;
  reads(turnId: string, stepNumber: number, payload: JsonObject): Promise<JsonObject>;
  complete(turnId: string, payload: JsonObject): Promise<JsonObject>;
  fail(turnId: string, payload: JsonObject): Promise<JsonObject>;
}
interface Options { baseUrl?: string; token?: string; timeoutMs?: number; fetchImpl?: typeof fetch }

const uuid = (value: unknown): value is string => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
const bounded = (value: unknown, max: number): value is string => typeof value === 'string' && value.length > 0 && value.length <= max;

function parseResponseObject(responseBody: string): JsonObject {
  try {
    const parsedResponse: unknown = responseBody ? JSON.parse(responseBody) : {};
    if (!parsedResponse || typeof parsedResponse !== 'object' || Array.isArray(parsedResponse)) throw new AdminAICallbackError('CALLBACK_INVALID_RESPONSE', false);
    return parsedResponse as JsonObject;
  } catch (error) {
    if (error instanceof AdminAICallbackError) throw error;
    if (error instanceof SyntaxError) throw new AdminAICallbackError('CALLBACK_INVALID_RESPONSE', false);
    throw error;
  }
}

export function validateAdminAIClaim(value: unknown, expectedTurnId: string): AdminAIClaimContext {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new AdminAICallbackError('CALLBACK_INVALID_RESPONSE', false);
  const claim = value as Partial<AdminAIClaimContext>;
  if (claim.schemaVersion !== '1' || claim.turnId !== expectedTurnId || !uuid(claim.turnId) || !uuid(claim.conversationId) || !uuid(claim.actorAdminUserId)) throw new AdminAICallbackError('CALLBACK_INVALID_RESPONSE', false);
  if (![claim.stepNumber, claim.expectedTurnVersion, claim.expectedConversationVersion, claim.expectedSecurityVersion].every(value => Number.isSafeInteger(value) && Number(value) >= 0)) throw new AdminAICallbackError('CALLBACK_INVALID_RESPONSE', false);
  if (!bounded(claim.leaseToken, 500) || !bounded(claim.callbackIdempotencyKey, 100) || !bounded(claim.systemInstructions, 52_000)) throw new AdminAICallbackError('CALLBACK_INVALID_RESPONSE', false);
  if (![claim.leaseExpiresAt, claim.deadlineAt].every(value => bounded(value, 100) && Number.isFinite(Date.parse(value as string)) && Date.parse(value as string) > Date.now())) throw new AdminAICallbackError('CALLBACK_INVALID_RESPONSE', false);
  if (!Array.isArray(claim.messages) || claim.messages.length > 100 || !Array.isArray(claim.readTools) || claim.readTools.length > 100 || !Array.isArray(claim.actionTools) || claim.actionTools.length > 300 || !claim.budgets || typeof claim.budgets !== 'object') throw new AdminAICallbackError('CALLBACK_INVALID_RESPONSE', false);
  if (!claim.capabilityBaseline || !uuid(claim.capabilityBaseline.id) || !bounded(claim.capabilityBaseline.version, 100) || !bounded(claim.capabilityBaseline.manifestHash, 128) || !claim.sensitiveDataPolicy || !uuid(claim.sensitiveDataPolicy.id) || !bounded(claim.sensitiveDataPolicy.version, 100) || !bounded(claim.sensitiveDataPolicy.policyHash, 128)) throw new AdminAICallbackError('CALLBACK_INVALID_RESPONSE', false);
  return claim as AdminAIClaimContext;
}

export function createAdminAICallbackClient(options: Options = {}): AdminAICallbackClient {
  const token = options.token ?? process.env.AI_CALLBACK_SECRET;
  if (isUnsafeSecret(token, 32)) throw new Error('AI_CALLBACK_SECRET is missing, weak, or unsafe.');
  const rawBase = options.baseUrl ?? process.env.BACKEND_API_URL ?? 'http://localhost:5245';
  const baseUrl = `${rawBase.replace(/\/$/, '').replace(/\/api\/v1$/, '')}/api/v1/internal/admin-ai`;
  const timeoutMs = options.timeoutMs ?? 10_000;

  async function request(path: string, body: JsonObject): Promise<{ status: number; value: JsonObject }> {
    let controller: AbortController | undefined; let timer: NodeJS.Timeout | undefined;
    try {
      const headers = { 'Content-Type': 'application/json', 'X-Internal-Token': token! };
      let response: Response;
      if (options.fetchImpl) {
        controller = new AbortController(); timer = setTimeout(() => controller?.abort(), timeoutMs);
        response = await options.fetchImpl(`${baseUrl}${path}`, { method: 'POST', headers, body: JSON.stringify(body), signal: controller.signal });
      } else response = await fetchWithTimeout(`${baseUrl}${path}`, { method: 'POST', headers, body: JSON.stringify(body), timeoutMs, maxResponseBytes: MAX_RESPONSE_BYTES });
      const declared = Number(response.headers.get('content-length'));
      if (Number.isFinite(declared) && declared > MAX_RESPONSE_BYTES) throw new AdminAICallbackError('CALLBACK_RESPONSE_TOO_LARGE', false);
      const bytes = new Uint8Array(await response.arrayBuffer());
      if (bytes.byteLength > MAX_RESPONSE_BYTES) throw new AdminAICallbackError('CALLBACK_RESPONSE_TOO_LARGE', false);
      const text = new TextDecoder().decode(bytes);
      if (!response.ok) {
        if (response.status === 404 && path.endsWith('/claim')) return { status: 404, value: {} };
        throw new AdminAICallbackError('CALLBACK_REJECTED', response.status === 408 || response.status === 429 || response.status >= 500, response.status);
      }
      return { status: response.status, value: parseResponseObject(text) };
    } catch (error) {
      if (error instanceof AdminAICallbackError) throw error;
      if (error instanceof WorkerExternalError && error.category === 'response-too-large') throw new AdminAICallbackError('CALLBACK_RESPONSE_TOO_LARGE', false);
      if (controller?.signal.aborted || (error instanceof WorkerExternalError && error.category === 'timeout')) throw new AdminAICallbackError('CALLBACK_TIMEOUT', true);
      throw new AdminAICallbackError('CALLBACK_UNAVAILABLE', true);
    } finally { if (timer) clearTimeout(timer); }
  }

  return {
    async claim(turnId, workerInstanceId) { const result = await request(`/turns/${encodeURIComponent(turnId)}/claim`, { schemaVersion: '1', workerInstanceId }); return result.status === 404 ? null : validateAdminAIClaim(result.value, turnId); },
    async renew(turnId, payload) { return (await request(`/turns/${encodeURIComponent(turnId)}/lease/renew`, payload)).value; },
    async reads(turnId, stepNumber, payload) { return (await request(`/turns/${encodeURIComponent(turnId)}/steps/${stepNumber}/reads`, payload)).value; },
    async complete(turnId, payload) { return (await request(`/turns/${encodeURIComponent(turnId)}/complete`, payload)).value; },
    async fail(turnId, payload) { return (await request(`/turns/${encodeURIComponent(turnId)}/fail`, payload)).value; },
  };
}

import { createHash } from 'node:crypto';

export const ADMIN_AI_SCHEMA_VERSION = '1' as const;
export const ADMIN_AI_DECISION_TYPES = ['answer', 'clarify', 'request_reads', 'propose_actions', 'refuse'] as const;
export type JsonObject = Record<string, unknown>;
export type AdminAIDecision =
  | { schemaVersion: '1'; type: 'answer'; answer: { summaryAr: string; facts: string[]; calculations: string[]; inferences: string[]; limitations: string[]; suggestions: string[]; evidenceInvocationIds: string[] } }
  | { schemaVersion: '1'; type: 'clarify'; clarification: { questionAr: string; reasonCode: 'AMBIGUOUS_TARGET' | 'AMBIGUOUS_SCOPE' | 'AMBIGUOUS_PERIOD' | 'AMBIGUOUS_METRIC' | 'MISSING_REQUIRED_INPUT'; options: Array<{ labelAr: string; value: string }> } }
  | { schemaVersion: '1'; type: 'request_reads'; calls: Array<{ callId: string; capabilityKey: string }> }
  | { schemaVersion: '1'; type: 'propose_actions'; messageAr: string; actions: Array<{ clientActionId: string; capabilityKey: string; arguments: JsonObject; safeIntentAr: string }> }
  | { schemaVersion: '1'; type: 'refuse'; refusal: { reasonCode: 'PROHIBITED_SECRET' | 'UNKNOWN_CAPABILITY' | 'POLICY_BYPASS' | 'RAW_DATABASE' | 'INFRASTRUCTURE' | 'UNSAFE_ATTACHMENT' | 'OUT_OF_SCOPE'; messageAr: string } };

export class AdminAIDecisionValidationError extends Error {
  readonly code = 'INVALID_ADMIN_AI_DECISION';
  constructor() { super('The AI provider returned an invalid admin-agent decision.'); this.name = 'AdminAIDecisionValidationError'; }
}
function invalid(): never { throw new AdminAIDecisionValidationError(); }
function object(value: unknown): JsonObject { if (!value || typeof value !== 'object' || Array.isArray(value)) invalid(); return value as JsonObject; }
function exact(value: JsonObject, keys: string[]) { if (Object.keys(value).length !== keys.length || keys.some(key => !(key in value))) invalid(); }
function text(value: unknown, max: number): string { if (typeof value !== 'string') invalid(); const result = value.trim(); if (!result || result.length > max) invalid(); return result; }
function texts(value: unknown, maxItems: number, maxLength = 1000): string[] { if (!Array.isArray(value) || value.length > maxItems) invalid(); return value.map(item => text(item, maxLength)); }
function boundedJson(value: unknown, depth = 0): unknown {
  if (depth > 4) invalid();
  if (value === null || typeof value === 'boolean') return value;
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') { if (value.length > 500) invalid(); return value; }
  if (Array.isArray(value)) { if (value.length > 20) invalid(); return value.map(item => boundedJson(item, depth + 1)); }
  const candidate = object(value); const keys = Object.keys(candidate);
  if (keys.length > 20 || keys.some(key => !key || key.length > 100)) invalid();
  return Object.fromEntries(keys.map(key => [key, boundedJson(candidate[key], depth + 1)]));
}

export function parseAdminAIDecision(input: unknown): AdminAIDecision {
  let encoded: string;
  try { encoded = JSON.stringify(input); } catch { invalid(); }
  if (Buffer.byteLength(encoded, 'utf8') > 65_536) invalid();
  const root = object(input);
  if (root.schemaVersion !== '1' || typeof root.type !== 'string' || !ADMIN_AI_DECISION_TYPES.includes(root.type as typeof ADMIN_AI_DECISION_TYPES[number])) invalid();
  switch (root.type) {
    case 'answer': {
      exact(root, ['schemaVersion', 'type', 'answer']); const answer = object(root.answer);
      exact(answer, ['summaryAr', 'facts', 'calculations', 'inferences', 'limitations', 'suggestions', 'evidenceInvocationIds']);
      return { schemaVersion: '1', type: 'answer', answer: { summaryAr: text(answer.summaryAr, 4000), facts: texts(answer.facts, 50), calculations: texts(answer.calculations, 30), inferences: texts(answer.inferences, 20), limitations: texts(answer.limitations, 20), suggestions: texts(answer.suggestions, 20), evidenceInvocationIds: texts(answer.evidenceInvocationIds, 100, 100) } };
    }
    case 'clarify': {
      exact(root, ['schemaVersion', 'type', 'clarification']); const clarification = object(root.clarification); exact(clarification, ['questionAr', 'reasonCode', 'options']);
      const reasons = ['AMBIGUOUS_TARGET', 'AMBIGUOUS_SCOPE', 'AMBIGUOUS_PERIOD', 'AMBIGUOUS_METRIC', 'MISSING_REQUIRED_INPUT'] as const;
      if (!reasons.includes(clarification.reasonCode as typeof reasons[number]) || !Array.isArray(clarification.options) || clarification.options.length > 3) invalid();
      const options = clarification.options.map(item => { const option = object(item); exact(option, ['labelAr', 'value']); return { labelAr: text(option.labelAr, 200), value: text(option.value, 200) }; });
      return { schemaVersion: '1', type: 'clarify', clarification: { questionAr: text(clarification.questionAr, 2000), reasonCode: clarification.reasonCode as typeof reasons[number], options } };
    }
    case 'request_reads': {
      exact(root, ['schemaVersion', 'type', 'calls']); if (!Array.isArray(root.calls) || root.calls.length < 1 || root.calls.length > 4) invalid();
      return { schemaVersion: '1', type: 'request_reads', calls: root.calls.map(item => { const call = object(item); exact(call, ['callId', 'capabilityKey']); return { callId: text(call.callId, 160), capabilityKey: text(call.capabilityKey, 160) }; }) };
    }
    case 'propose_actions': {
      exact(root, ['schemaVersion', 'type', 'messageAr', 'actions']); if (!Array.isArray(root.actions) || root.actions.length < 1 || root.actions.length > 5) invalid();
      return { schemaVersion: '1', type: 'propose_actions', messageAr: text(root.messageAr, 2000), actions: root.actions.map(item => { const action = object(item); exact(action, ['clientActionId', 'capabilityKey', 'arguments', 'safeIntentAr']); return { clientActionId: text(action.clientActionId, 100), capabilityKey: text(action.capabilityKey, 160), arguments: boundedJson(action.arguments) as JsonObject, safeIntentAr: text(action.safeIntentAr, 1000) }; }) };
    }
    case 'refuse': {
      exact(root, ['schemaVersion', 'type', 'refusal']); const refusal = object(root.refusal); exact(refusal, ['reasonCode', 'messageAr']);
      const reasons = ['PROHIBITED_SECRET', 'UNKNOWN_CAPABILITY', 'POLICY_BYPASS', 'RAW_DATABASE', 'INFRASTRUCTURE', 'UNSAFE_ATTACHMENT', 'OUT_OF_SCOPE'] as const;
      if (!reasons.includes(refusal.reasonCode as typeof reasons[number])) invalid();
      return { schemaVersion: '1', type: 'refuse', refusal: { reasonCode: refusal.reasonCode as typeof reasons[number], messageAr: text(refusal.messageAr, 1000) } };
    }
    default: return invalid();
  }
}
function canonical(value: unknown): unknown { if (Array.isArray(value)) return value.map(canonical); if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value as JsonObject).sort(([a], [b]) => a.localeCompare(b)).map(([key, item]) => [key, canonical(item)])); return value; }
export const canonicalAdminAIDecision = (decision: AdminAIDecision) => JSON.stringify(canonical(decision));
export const hashAdminAIDecision = (decision: AdminAIDecision) => createHash('sha256').update(canonicalAdminAIDecision(decision), 'utf8').digest('hex');

import { createHash } from 'node:crypto';

export const LIVE_SUPPORT_DECISION_TYPES = [
  'reply',
  'propose_action',
  'request_verification',
  'propose_account_creation',
  'request_resolution',
  'handoff',
] as const;

export type LiveSupportDecisionType = typeof LIVE_SUPPORT_DECISION_TYPES[number];
export const LIVE_SUPPORT_SCHEMA_VERSION = '1' as const;

type JsonObject = Record<string, unknown>;

interface DecisionBase {
  schemaVersion: typeof LIVE_SUPPORT_SCHEMA_VERSION;
  messageAr?: string;
}

export type LiveSupportDecision =
  | (DecisionBase & { type: 'reply'; messageAr: string })
  | (DecisionBase & {
      type: 'propose_action';
      action: {
        key: string;
        arguments: JsonObject;
        safeEffectSummaryAr: string;
        safeConsequenceAr?: string;
      };
    })
  | (DecisionBase & { type: 'request_verification'; verification: { intent: 'existing_account' } })
  | (DecisionBase & { type: 'propose_account_creation'; accountCreation: { requestedFields: string[] } })
  | (DecisionBase & { type: 'request_resolution'; resolution: { reasonCode: string; safeSummaryAr: string } })
  | (DecisionBase & { type: 'handoff'; handoff: { reasonCode: string; safeSummaryAr: string; forced: false } });

export class LiveSupportDecisionValidationError extends Error {
  readonly code = 'INVALID_AI_DECISION';

  constructor() {
    super('The AI provider returned an invalid live-support decision.');
    this.name = 'LiveSupportDecisionValidationError';
  }
}

export function isLiveSupportDecisionType(value: unknown): value is LiveSupportDecisionType {
  return typeof value === 'string' && LIVE_SUPPORT_DECISION_TYPES.includes(value as LiveSupportDecisionType);
}

function invalid(): never {
  throw new LiveSupportDecisionValidationError();
}

function object(value: unknown): JsonObject {
  if (!value || typeof value !== 'object' || Array.isArray(value)) invalid();
  return value as JsonObject;
}

function exactKeys(value: JsonObject, required: string[], optional: string[] = []) {
  const allowed = new Set([...required, ...optional]);
  if (required.some(key => !(key in value)) || Object.keys(value).some(key => !allowed.has(key))) invalid();
}

function boundedString(value: unknown, max: number, min = 1): string {
  if (typeof value !== 'string') invalid();
  const normalized = value.trim();
  if (normalized.length < min || normalized.length > max) invalid();
  return normalized;
}

function optionalMessage(value: unknown): string | undefined {
  return value === undefined ? undefined : boundedString(value, 4000);
}

function boundedJson(value: unknown, depth = 0): unknown {
  if (depth > 4) invalid();
  if (value === null || typeof value === 'boolean') return value;
  if (typeof value === 'string') return boundedString(value, 500, 0);
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (Array.isArray(value)) {
    if (value.length > 20) invalid();
    return value.map(item => boundedJson(item, depth + 1));
  }
  const candidate = object(value);
  const keys = Object.keys(candidate);
  if (keys.length > 20 || keys.some(key => key.length === 0 || key.length > 100)) invalid();
  return Object.fromEntries(keys.map(key => [key, boundedJson(candidate[key], depth + 1)]));
}

export function parseLiveSupportDecision(input: unknown): LiveSupportDecision {
  const root = object(input);
  exactKeys(root, ['schemaVersion', 'type'], ['messageAr', 'action', 'verification', 'accountCreation', 'resolution', 'handoff']);
  if (root.schemaVersion !== LIVE_SUPPORT_SCHEMA_VERSION || !isLiveSupportDecisionType(root.type)) invalid();
  const messageAr = optionalMessage(root.messageAr);
  const base = { schemaVersion: LIVE_SUPPORT_SCHEMA_VERSION, ...(messageAr ? { messageAr } : {}) };

  switch (root.type) {
    case 'reply':
      if (root.action || root.verification || root.accountCreation || root.resolution || root.handoff || !messageAr) invalid();
      return { ...base, type: 'reply', messageAr };
    case 'propose_action': {
      if (root.verification || root.accountCreation || root.resolution || root.handoff) invalid();
      const action = object(root.action);
      exactKeys(action, ['key', 'arguments', 'safeEffectSummaryAr'], ['safeConsequenceAr']);
      const safeConsequenceAr = action.safeConsequenceAr === undefined ? undefined : boundedString(action.safeConsequenceAr, 1000);
      return { ...base, type: 'propose_action', action: {
        key: boundedString(action.key, 120),
        arguments: boundedJson(action.arguments) as JsonObject,
        safeEffectSummaryAr: boundedString(action.safeEffectSummaryAr, 1000),
        ...(safeConsequenceAr ? { safeConsequenceAr } : {}),
      } };
    }
    case 'request_verification': {
      if (root.action || root.accountCreation || root.resolution || root.handoff) invalid();
      const verification = object(root.verification);
      exactKeys(verification, ['intent']);
      if (verification.intent !== 'existing_account') invalid();
      return { ...base, type: 'request_verification', verification: { intent: 'existing_account' } };
    }
    case 'propose_account_creation': {
      if (root.action || root.verification || root.resolution || root.handoff) invalid();
      const accountCreation = object(root.accountCreation);
      exactKeys(accountCreation, ['requestedFields']);
      if (!Array.isArray(accountCreation.requestedFields) || accountCreation.requestedFields.length < 1 || accountCreation.requestedFields.length > 20) invalid();
      const requestedFields = accountCreation.requestedFields.map(field => boundedString(field, 100));
      if (new Set(requestedFields).size !== requestedFields.length) invalid();
      return { ...base, type: 'propose_account_creation', accountCreation: { requestedFields } };
    }
    case 'request_resolution': {
      if (root.action || root.verification || root.accountCreation || root.handoff) invalid();
      const resolution = object(root.resolution);
      exactKeys(resolution, ['reasonCode', 'safeSummaryAr']);
      return { ...base, type: 'request_resolution', resolution: {
        reasonCode: boundedString(resolution.reasonCode, 100),
        safeSummaryAr: boundedString(resolution.safeSummaryAr, 1000),
      } };
    }
    case 'handoff': {
      if (root.action || root.verification || root.accountCreation || root.resolution) invalid();
      const handoff = object(root.handoff);
      exactKeys(handoff, ['reasonCode', 'safeSummaryAr', 'forced']);
      if (handoff.forced !== false) invalid();
      return { ...base, type: 'handoff', handoff: {
        reasonCode: boundedString(handoff.reasonCode, 100),
        safeSummaryAr: boundedString(handoff.safeSummaryAr, 2000),
        forced: false,
      } };
    }
  }
}

function canonicalize(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonicalize);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value as JsonObject)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, item]) => [key, canonicalize(item)]));
  }
  return value;
}

export function canonicalLiveSupportDecision(decision: LiveSupportDecision): string {
  return JSON.stringify(canonicalize(decision));
}

export function hashLiveSupportDecision(decision: LiveSupportDecision): string {
  return createHash('sha256').update(canonicalLiveSupportDecision(decision), 'utf8').digest('hex');
}

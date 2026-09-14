export const ADMIN_AI_EVENT_TYPES = [
  'snapshot_changed',
  'access_revoked',
  'conversation.changed',
  'turn.changed',
  'proposal.changed',
  'execution.changed',
  'access.revoked',
] as const;
export type AdminAiEventType = (typeof ADMIN_AI_EVENT_TYPES)[number];

export interface AdminAiRealtimeEnvelope {
  schemaVersion: '1';
  eventId: string;
  sequence: number;
  type: AdminAiEventType;
  conversationId: string;
  turnId?: string;
  proposalId?: string;
  occurredAt: string;
}

const UUID =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function parseAdminAiRealtimeEnvelope(
  raw: unknown
): AdminAiRealtimeEnvelope | undefined {
  try {
    const value = typeof raw === 'string' ? JSON.parse(raw) : raw;
    if (!value || typeof value !== 'object' || Array.isArray(value))
      return undefined;
    const item = value as Record<string, unknown>;
    const allowed = new Set([
      'schemaVersion',
      'eventId',
      'sequence',
      'type',
      'conversationId',
      'turnId',
      'proposalId',
      'occurredAt',
    ]);
    if (Object.keys(item).some((key) => !allowed.has(key))) return undefined;
    if (
      item.schemaVersion !== '1' ||
      typeof item.type !== 'string' ||
      !ADMIN_AI_EVENT_TYPES.includes(item.type as AdminAiEventType)
    )
      return undefined;
    if (
      typeof item.eventId !== 'string' ||
      !UUID.test(item.eventId) ||
      typeof item.conversationId !== 'string' ||
      !UUID.test(item.conversationId)
    )
      return undefined;
    if (!Number.isSafeInteger(item.sequence) || (item.sequence as number) < 1)
      return undefined;
    if (
      item.turnId !== undefined &&
      (typeof item.turnId !== 'string' || !UUID.test(item.turnId))
    )
      return undefined;
    if (
      item.proposalId !== undefined &&
      (typeof item.proposalId !== 'string' || !UUID.test(item.proposalId))
    )
      return undefined;
    if (
      typeof item.occurredAt !== 'string' ||
      !Number.isFinite(Date.parse(item.occurredAt))
    )
      return undefined;
    return item as unknown as AdminAiRealtimeEnvelope;
  } catch {
    return undefined;
  }
}

export type AdminAiSequenceDecision = 'accept' | 'duplicate' | 'reconcile';
export function decideAdminAiSequence(
  previous: number,
  next: number
): AdminAiSequenceDecision {
  if (next <= previous) return 'duplicate';
  if (previous > 0 && next > previous + 1) return 'reconcile';
  return 'accept';
}

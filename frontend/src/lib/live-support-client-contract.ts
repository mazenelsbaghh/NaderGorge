export interface LiveSupportClientEnvelope {
  eventId: string;
  conversationId?: string;
  sequence?: number;
  type: string;
  payload: unknown;
}

export type LiveSupportSequenceDecision = 'accept' | 'duplicate' | 'reconcile';

export function parseLiveSupportEnvelope(raw: string | LiveSupportClientEnvelope): LiveSupportClientEnvelope | undefined {
  try {
    const value = typeof raw === 'string' ? JSON.parse(raw) : raw;
    if (!value || typeof value !== 'object') return undefined;
    const candidate = value as Partial<LiveSupportClientEnvelope>;
    if (typeof candidate.eventId !== 'string' || !candidate.eventId.trim()) return undefined;
    if (typeof candidate.type !== 'string' || !candidate.type.trim()) return undefined;
    if (candidate.conversationId !== undefined && typeof candidate.conversationId !== 'string') return undefined;
    if (candidate.sequence !== undefined && (!Number.isInteger(candidate.sequence) || candidate.sequence < 1)) return undefined;
    return candidate as LiveSupportClientEnvelope;
  } catch {
    return undefined;
  }
}

export function decideLiveSupportSequence(previous: number, next?: number): LiveSupportSequenceDecision {
  if (next === undefined) return 'accept';
  if (next <= previous) return 'duplicate';
  if (previous > 0 && next > previous + 1) return 'reconcile';
  return 'accept';
}

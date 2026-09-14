import type { StaffDataChangedPayload } from '@/lib/staff-realtime-scopes';

const allowedOperations = new Set(['created', 'updated', 'deleted', 'bulk']);

export function parseStaffDataChangedPayload(value: unknown): StaffDataChangedPayload | null {
  if (!value || typeof value !== 'object') return null;
  const candidate = value as Partial<StaffDataChangedPayload>;
  if (!Array.isArray(candidate.scopes) || candidate.scopes.some((scope) => typeof scope !== 'string')) return null;
  if (candidate.operation && !allowedOperations.has(candidate.operation)) return null;
  if (candidate.entityIds && (!Array.isArray(candidate.entityIds) || candidate.entityIds.some((id) => typeof id !== 'string'))) return null;
  return { ...candidate, scopes: [...new Set(candidate.scopes)] } as StaffDataChangedPayload;
}

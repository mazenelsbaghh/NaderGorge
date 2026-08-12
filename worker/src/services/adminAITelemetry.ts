export type AdminAIMetric = 'queue_age' | 'model_latency' | 'model_outcome' | 'read_outcome' | 'proposal_count' | 'execution_outcome' | 'recovery_outcome';
type DimensionValue = string | number | boolean;
const allowedDimensions = new Set(['queue', 'outcome', 'provider', 'model', 'decisionType', 'capabilityKey', 'status', 'failureCode', 'countBucket', 'latencyBucket']);
const identifier = /^[a-zA-Z0-9._:-]{1,80}$/;
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function safeAdminAITelemetryLabel(label: string) {
  return identifier.test(label) && !label.includes('@') && !uuid.test(label) ? label : 'other';
}

function validateFields(fields: Record<string, DimensionValue>) {
  for (const [field, fieldValue] of Object.entries(fields)) {
    if (!allowedDimensions.has(field)) throw new Error('UNSAFE_ADMIN_AI_TELEMETRY_DIMENSION');
    if (typeof fieldValue === 'string' && safeAdminAITelemetryLabel(fieldValue) !== fieldValue) throw new Error('HIGH_CARDINALITY_ADMIN_AI_TELEMETRY_VALUE');
  }
}

export function recordAdminAIMetric(metric: AdminAIMetric, value: number, dimensions: Record<string, DimensionValue> = {}) {
  if (!Number.isFinite(value)) throw new Error('INVALID_ADMIN_AI_TELEMETRY_VALUE');
  validateFields(dimensions);
  console.info('[AdminAIMetric]', { metric, value, ...dimensions });
}

export function logAdminAIEvent(event: 'worker_started' | 'turn_completed' | 'turn_failed' | 'callback_replayed', fields: Record<string, DimensionValue> = {}) {
  validateFields(fields);
  console.info('[AdminAIEvent]', { event, ...fields });
}

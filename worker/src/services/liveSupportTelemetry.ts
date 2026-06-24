export type SafeLiveSupportMetric = 'queue_age' | 'inference_latency' | 'callback_outcome' | 'recovery_outcome';

export function recordLiveSupportMetric(name: SafeLiveSupportMetric, value: number, dimensions: Record<string, string | number | boolean> = {}) {
  const forbidden = /prompt|message|name|phone|password|answer|token|secret/i;
  if (Object.keys(dimensions).some(key => forbidden.test(key))) throw new Error('UNSAFE_TELEMETRY_DIMENSION');
  console.info('[LiveSupportMetric]', { name, value, ...dimensions });
}

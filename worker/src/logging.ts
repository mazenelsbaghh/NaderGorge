export function maskId(value: unknown) {
  const text = String(value || '');
  if (text.length <= 8) return text || 'unknown';
  return `${text.slice(0, 4)}...${text.slice(-4)}`;
}

export function logQueueEvent(queueName: string, message: string, details: Record<string, unknown> = {}) {
  const safeDetails = Object.fromEntries(
    Object.entries(details).map(([key, value]) => [key, key.toLowerCase().includes('id') ? maskId(value) : value])
  );
  console.log(`[${queueName}] ${message}`, safeDetails);
}

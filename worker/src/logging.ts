export function maskId(value: unknown) {
  const text = String(value || '');
  if (text.length <= 8) return text || 'unknown';
  return `${text.slice(0, 4)}...${text.slice(-4)}`;
}

const SENSITIVE_KEY_PATTERN = /(token|secret|password|hash|code|url|response|prompt|answer|content|text)/i;
const URL_PATTERN = /\bhttps?:\/\/[^\s]+/gi;

function redactValue(key: string, value: unknown): unknown {
  if (value == null) return value;

  if (key.toLowerCase().includes('id')) {
    return maskId(value);
  }

  if (SENSITIVE_KEY_PATTERN.test(key)) {
    return '[redacted]';
  }

  if (typeof value === 'string') {
    const withoutUrls = value.replace(URL_PATTERN, '[redacted-url]');
    return withoutUrls.length > 160 ? `${withoutUrls.slice(0, 160)}...` : withoutUrls;
  }

  if (Array.isArray(value)) {
    return `[array:${value.length}]`;
  }

  if (typeof value === 'object') {
    return '[object]';
  }

  return value;
}

export function logQueueEvent(queueName: string, message: string, details: Record<string, unknown> = {}) {
  const safeDetails = Object.fromEntries(
    Object.entries(details).map(([key, value]) => [key, redactValue(key, value)])
  );
  console.log(`[${queueName}] ${message}`, safeDetails);
}

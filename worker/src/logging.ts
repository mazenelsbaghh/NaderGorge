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

export function logInfo(scope: string, message: string, details: Record<string, unknown> = {}) {
  logQueueEvent(scope, message, details);
}

export function logWarn(scope: string, message: string, details: Record<string, unknown> = {}) {
  const safeDetails = Object.fromEntries(
    Object.entries(details).map(([key, value]) => [key, redactValue(key, value)])
  );
  console.warn(`[${scope}] ${message}`, safeDetails);
}

export function logError(scope: string, message: string, details: Record<string, unknown> = {}) {
  const safeDetails = Object.fromEntries(
    Object.entries(details).map(([key, value]) => [key, redactValue(key, value)])
  );
  console.error(`[${scope}] ${message}`, safeDetails);
}

export function installSystemLogCapture(redis: Redis) {
  const originalWarn = console.warn.bind(console);
  const originalError = console.error.bind(console);

  console.warn = (...args: unknown[]) => {
    originalWarn(...args);
    storeSystemLog(redis, 'warning', args);
  };
  console.error = (...args: unknown[]) => {
    originalError(...args);
    storeSystemLog(redis, 'error', args);
  };
}

function storeSystemLog(redis: Redis, level: 'warning' | 'error', args: unknown[]) {
  const message = args.map(formatLogArgument).join(' ').slice(0, 12_000);
  const categoryMatch = message.match(/^\[([^\]]+)]/);
  const entry = JSON.stringify({
    id: crypto.randomUUID(),
    timestamp: new Date().toISOString(),
    source: 'worker',
    level,
    category: categoryMatch?.[1] || 'Worker',
    message,
    exception: null,
  });

  void redis.multi().rpush(SYSTEM_LOG_KEY, entry).ltrim(SYSTEM_LOG_KEY, -SYSTEM_LOG_CAPACITY, -1).exec()
    .catch(() => undefined);
}

function formatLogArgument(value: unknown): string {
  if (value instanceof Error) return redactText(value.stack || value.message);
  if (typeof value === 'string') return redactText(value);
  try { return redactText(JSON.stringify(value)); }
  catch { return '[unserializable]'; }
}

function redactText(value: string) {
  return value
    .replace(/\bhttps?:\/\/[^\s]+/gi, '[redacted-url]')
    .replace(/(token|secret|password|authorization|cookie)\s*[:=]\s*[^\s,;]+/gi, '$1=[redacted]');
}
import type { Redis } from 'ioredis';
import crypto from 'node:crypto';

const SYSTEM_LOG_KEY = 'system:logs:v1';
const SYSTEM_LOG_CAPACITY = 2_000;

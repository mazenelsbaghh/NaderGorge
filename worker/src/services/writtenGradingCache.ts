import { createHash, randomUUID } from 'node:crypto';
import { setTimeout as delay } from 'node:timers/promises';
import type { Redis } from 'ioredis';
import { GeminiDeveloperApiError } from './aiProvider.js';
import { AI_PROVIDER_STATUS_KEY } from './aiProviderMonitor.js';

const CACHE_SECONDS = 24 * 60 * 60;
const GENERATION_LEASE_MS = 75_000;
let connection: Redis | undefined;

export function installWrittenGradingCache(redis?: Redis) {
  connection = redis;
}

async function cacheCommand<T>(operation: () => Promise<T>): Promise<T | undefined> {
  try { return await operation(); }
  catch (error) {
    if (!(error instanceof Error)) throw error;
    // Cache availability must not discard a paid, validated grade.
    console.warn('[written-grading-cache] Cache unavailable.', { errorName: error.name });
    return undefined;
  }
}

async function readCached<T>(redis: Redis, key: string): Promise<{ result: T } | undefined> {
  const serialized = await cacheCommand(() => redis.get(key));
  if (!serialized) return undefined;
  try {
    const entry = JSON.parse(serialized) as { result?: T } | null;
    if (entry && typeof entry === 'object' && Object.hasOwn(entry, 'result')) return entry as { result: T };
    await cacheCommand(() => redis.del(key));
    return undefined;
  }
  catch (error) {
    if (!(error instanceof SyntaxError)) throw error;
    await cacheCommand(() => redis.del(key));
    return undefined;
  }
}

async function checkBillingCircuit(redis: Redis) {
  const status = await cacheCommand(() => redis.get(AI_PROVIDER_STATUS_KEY));
  if (!status) return;
  let observation: { state?: string; updatedAt?: string };
  try { observation = JSON.parse(status); }
  catch (error) { if (error instanceof SyntaxError) return; throw error; }
  // Permit a fresh provider probe after a minute so recharged credit recovers automatically.
  if (observation?.state === 'balance-exhausted'
    && Date.now() - Date.parse(observation.updatedAt ?? '') < 60_000) {
    throw new GeminiDeveloperApiError('balance-exhausted');
  }
}

const PUBLISH_OWNED_RESULT = `
if redis.call('GET', KEYS[2]) ~= ARGV[1] then return 0 end
redis.call('SET', KEYS[1], ARGV[2], 'EX', ARGV[3])
return 1
`;
const RELEASE_OWNED_LOCK = `
if redis.call('GET', KEYS[1]) ~= ARGV[1] then return 0 end
return redis.call('DEL', KEYS[1])
`;

export async function cachedWrittenGrading<T>(
  identity: object, generate: () => Promise<T>, provider: 'gemini' | 'openai' = 'gemini',
): Promise<T> {
  const redis = connection;
  if (!redis) return generate();
  const digest = createHash('sha256').update(JSON.stringify(identity)).digest('hex');
  const key = `ai:written-cache:v1:${digest}`;
  const lockKey = `${key}:lock`;
  const owner = randomUUID();
  const deadline = Date.now() + GENERATION_LEASE_MS + 1_000;
  while (Date.now() < deadline) {
    const saved = await readCached<T>(redis, key);
    if (saved) {
      console.log('[written-grading-cache]', { outcome: 'hit' });
      return saved.result;
    }
    const acquired = await cacheCommand(() => redis.set(lockKey, owner, 'PX', GENERATION_LEASE_MS, 'NX'));
    if (acquired === undefined) return generate();
    if (acquired === 'OK') return generateAndCache(redis, { key, lockKey, owner, provider }, generate);
    await delay(100);
  }
  throw new Error('Written grading is already in progress; retry after the current generation.');
}

async function generateAndCache<T>(
  redis: Redis,
  lease: { key: string; lockKey: string; owner: string; provider: 'gemini' | 'openai' },
  generate: () => Promise<T>,
): Promise<T> {
  try {
    // The lock may have been released between our cache read and acquisition.
    const saved = await readCached<T>(redis, lease.key);
    if (saved) return saved.result;
    if (lease.provider === 'gemini') await checkBillingCircuit(redis);
    console.log('[written-grading-cache]', { outcome: 'miss' });
    const result = await generate();
    const serialized = JSON.stringify({ result });
    if (Buffer.byteLength(serialized) <= 128 * 1024) {
      await cacheCommand(() => redis.eval(PUBLISH_OWNED_RESULT, 2,
        lease.key, lease.lockKey, lease.owner, serialized, CACHE_SECONDS));
    }
    return result;
  } finally {
    await cacheCommand(() => redis.eval(RELEASE_OWNED_LOCK, 1, lease.lockKey, lease.owner));
  }
}

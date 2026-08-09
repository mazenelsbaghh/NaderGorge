import { Redis, type RedisOptions } from 'ioredis';

function parseSentinel(value: string) {
  const separator = value.lastIndexOf(':');
  if (separator < 1) {
    throw new Error(`Invalid Redis Sentinel endpoint: ${value}`);
  }
  const host = value.slice(0, separator).trim();
  const port = Number(value.slice(separator + 1));
  if (!host || !Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error(`Invalid Redis Sentinel endpoint: ${value}`);
  }
  return { host, port };
}

export function redisConnectionOptions(overrides: RedisOptions = {}): RedisOptions {
  const sentinelList = process.env.REDIS_SENTINELS
    ?.split(',')
    .map((item) => item.trim())
    .filter(Boolean);
  const serviceName = process.env.REDIS_SENTINEL_MASTER;

  if (sentinelList?.length && serviceName) {
    const password = process.env.REDIS_PASSWORD;
    if (!password) {
      throw new Error('REDIS_PASSWORD is required when Redis Sentinel is enabled.');
    }
    return {
      sentinels: sentinelList.map(parseSentinel),
      name: serviceName,
      role: 'master',
      password,
      sentinelPassword: password,
      enableReadyCheck: true,
      maxRetriesPerRequest: null,
      connectTimeout: 10_000,
      sentinelRetryStrategy: attempt => Math.min(250 * 2 ** Math.min(attempt - 1, 5), 8_000),
      ...overrides,
    };
  }

  const redisUrl = process.env.REDIS_URL;
  if (!redisUrl && process.env.NODE_ENV === 'production') {
    throw new Error('Redis Sentinel configuration is required in production.');
  }
  const parsed = new URL(redisUrl || 'redis://localhost:6379');
  return {
    host: parsed.hostname,
    port: Number(parsed.port) || 6379,
    username: parsed.username || undefined,
    password: parsed.password || undefined,
    maxRetriesPerRequest: null,
    ...overrides,
  };
}

export function createRedisConnection(overrides: RedisOptions = {}) {
  return new Redis(redisConnectionOptions(overrides));
}

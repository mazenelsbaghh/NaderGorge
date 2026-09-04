import assert from 'node:assert/strict';
import test from 'node:test';
import { databasePool, databasePoolConfig, databaseUrl } from './config/database.js';
import { redisConnectionOptions } from './config/redis.js';

function withEnvironment(
  updates: Record<string, string | undefined>,
  action: () => void,
) {
  const previous = Object.fromEntries(
    Object.keys(updates).map((key) => [key, process.env[key]]),
  );
  try {
    for (const [key, value] of Object.entries(updates)) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
    action();
  } finally {
    for (const [key, value] of Object.entries(previous)) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
  }
}

test('production database refuses a local fallback', () => {
  withEnvironment({
    NODE_ENV: 'production',
    DATABASE_URL: undefined,
    DB_CONNECTION_STRING: undefined,
  }, () => {
    assert.throws(() => databaseUrl(), /required in production/);
  });
});

test('worker database pool budget leaves production connection headroom', () => {
  withEnvironment({
    NODE_ENV: 'production',
    DATABASE_URL: 'postgresql://massar_app:test@database/massar_platform',
    DB_CONNECTION_STRING: undefined,
    MASSAR_NODE_ID: 'node-2',
  }, () => {
    const config = databasePoolConfig();
    assert.equal(config.max, 10);
    assert.equal(config.idleTimeoutMillis, 60_000);
    assert.equal(config.connectionTimeoutMillis, 15_000);
    assert.equal(config.application_name, 'massar-worker-node-2');
  });
});

test('2026-09-04 every worker job module receives the same database pool', async () => {
  let firstPool!: ReturnType<typeof databasePool>;
  let secondPool!: ReturnType<typeof databasePool>;
  withEnvironment({
    NODE_ENV: 'production',
    DATABASE_URL: 'postgresql://massar_app:test@database/massar_platform',
    DB_CONNECTION_STRING: undefined,
  }, () => {
    firstPool = databasePool();
    secondPool = databasePool();
  });

  assert.strictEqual(firstPool, secondPool);
  await firstPool.end();
});

test('production Redis discovers one master through all three Sentinels', () => {
  withEnvironment({
    NODE_ENV: 'production',
    REDIS_URL: undefined,
    REDIS_SENTINELS: '10.77.0.11:26379,10.77.0.12:26379,10.77.0.13:26379',
    REDIS_SENTINEL_MASTER: 'massar-redis',
    REDIS_PASSWORD: 'test-only-strong-redis-password',
  }, () => {
    const options = redisConnectionOptions();
    assert.equal(options.name, 'massar-redis');
    assert.equal(options.role, 'master');
    assert.equal(options.sentinels?.length, 3);
    assert.equal(options.maxRetriesPerRequest, null);
  });
});

test('production Redis refuses direct localhost fallback', () => {
  withEnvironment({
    NODE_ENV: 'production',
    REDIS_URL: undefined,
    REDIS_SENTINELS: undefined,
    REDIS_SENTINEL_MASTER: undefined,
    REDIS_PASSWORD: undefined,
  }, () => {
    assert.throws(() => redisConnectionOptions(), /Sentinel configuration/);
  });
});

import test, { type TestContext } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { setTimeout as delay } from 'node:timers/promises';
import { createRedisConnection } from '../config/redis.js';
import { cachedWrittenGrading, installWrittenGradingCache } from './writtenGradingCache.js';
import { GeminiDeveloperApiError } from './aiProvider.js';
import { AI_PROVIDER_STATUS_KEY } from './aiProviderMonitor.js';

const integration = { skip: process.env.RUN_WRITTEN_CACHE_REDIS_TESTS !== '1' };
const identity = { model: 'gemini-3.1-flash-lite', policy: 'policy1', question: 'Question', reference: 'Reference', answer: 'Answer' };

function connect(context: TestContext) {
  const prefix = `test-written-cache-${randomUUID()}:`;
  const redis = createRedisConnection({ keyPrefix: prefix });
  installWrittenGradingCache(redis);
  context.after(async () => {
    installWrittenGradingCache();
    const cleanup = createRedisConnection();
    let cursor = '0';
    do {
      const page = await cleanup.scan(cursor, 'MATCH', `${prefix}*`, 'COUNT', 100);
      cursor = page[0];
      if (page[1].length) await cleanup.unlink(...page[1]);
    } while (cursor !== '0');
    await Promise.all([redis.quit(), cleanup.quit()]);
  });
  return redis;
}

test('2026-11-02 concurrent grading shares one paid result and later delivery survives provider failure', integration, async context => {
  connect(context);
  let paidGenerations = 0;
  const grade = { score: 0.75, feedback: 'Validated partial credit' };
  const results = await Promise.all(Array.from({ length: 9 }, () => cachedWrittenGrading(identity, async () => {
    paidGenerations++;
    await delay(30);
    return grade;
  })));
  assert.equal(paidGenerations, 1);
  assert.deepEqual(results, Array.from({ length: 9 }, () => grade));
  assert.deepEqual(await cachedWrittenGrading(identity, async () => { throw new Error('Provider unavailable'); }), grade);
});

test('changed answer, reference, question, model or policy never reuses another grade; failed results are not cached', integration, async context => {
  connect(context);
  await cachedWrittenGrading(identity, async () => ({ marker: 'original' }));
  for (const field of Object.keys(identity)) {
    const changed = { ...identity, [field]: `different-${field}` };
    await assert.rejects(cachedWrittenGrading(changed, async () => { throw new Error('Provider rejected'); }), /Provider rejected/);
    assert.deepEqual(await cachedWrittenGrading(changed, async () => ({ marker: field })), { marker: field });
  }
});

test('recent exhausted balance blocks new paid requests while cached grades and later recharge probes remain usable', integration, async context => {
  const redis = connect(context);
  const grade = { score: 1, feedback: 'Saved' };
  await cachedWrittenGrading(identity, async () => grade);
  await redis.set(AI_PROVIDER_STATUS_KEY, JSON.stringify({ state: 'balance-exhausted', updatedAt: new Date().toISOString() }));
  assert.deepEqual(await cachedWrittenGrading(identity, async () => { throw new Error('Must use cache'); }), grade);
  const pending = { ...identity, answer: 'New answer' };
  await assert.rejects(cachedWrittenGrading(pending, async () => { throw new Error('Must not charge'); }),
    error => error instanceof GeminiDeveloperApiError && error.category === 'balance-exhausted');
  await redis.set(AI_PROVIDER_STATUS_KEY, JSON.stringify({ state: 'balance-exhausted', updatedAt: new Date(Date.now() - 61_000).toISOString() }));
  assert.deepEqual(await cachedWrittenGrading(pending, async () => grade), grade);
});

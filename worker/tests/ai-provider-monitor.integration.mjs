import { test } from 'node:test';
import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';
import { Redis } from 'ioredis';
import { installAIProviderMonitor, AI_PROVIDER_STATUS_KEY } from '../dist/services/aiProviderMonitor.js';
import { executeGeminiRequest, setAIProviderObserver } from '../dist/services/aiProvider.js';

const url = new URL(process.env.TEST_REDIS_URL || '');
if (!['127.0.0.1', 'localhost'].includes(url.hostname)) throw new Error('Use an isolated local Redis for this test.');

test('provider billing alerts survive duplicate failures, ignore stale successes and clear on recovery', async context => {
  const redis = new Redis(url.toString());
  context.after(async () => { setAIProviderObserver(); await redis.del(AI_PROVIDER_STATUS_KEY); await redis.quit(); });
  await redis.del(AI_PROVIDER_STATUS_KEY);
  installAIProviderMonitor(redis);
  async function statusIs(state) {
    for (let index = 0; index < 100; index++) {
      const json = await redis.get(AI_PROVIDER_STATUS_KEY);
      const status = json ? JSON.parse(json) : null;
      if (status?.state === state) return status;
      await delay(20);
    }
    assert.fail(`Provider state did not reach ${state}`);
  }
  let finishOld;
  const old = executeGeminiRequest(() => new Promise(resolve => { finishOld = resolve; }));
  await delay(5);
  await assert.rejects(executeGeminiRequest(async () => { throw { status: 429, message: 'Your prepayment credits are depleted. PRIVATE_PROVIDER_DATA' }; }));
  const incident = await statusIs('balance-exhausted');
  assert.ok(incident.incidentId);
  assert.equal(JSON.stringify(incident).includes('PRIVATE_PROVIDER_DATA'), false);
  finishOld('old successful result');
  await old;
  await delay(30);
  assert.equal((await statusIs('balance-exhausted')).incidentId, incident.incidentId);
  await assert.rejects(executeGeminiRequest(async () => { throw { status: 402 }; }));
  await delay(30);
  assert.equal((await statusIs('balance-exhausted')).incidentId, incident.incidentId);
  await executeGeminiRequest(async () => 'service restored');
  await statusIs('healthy');
  await delay(5);
  await assert.rejects(executeGeminiRequest(async () => { throw { status: 429, message: 'RPM quota exceeded' }; }));
  const quota = await statusIs('quota-exhausted');
  assert.notEqual(quota.incidentId, incident.incidentId);
});

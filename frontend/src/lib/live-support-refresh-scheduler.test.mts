import assert from 'node:assert/strict';
import test from 'node:test';
import { createLiveSupportRefreshScheduler } from './live-support-refresh-scheduler.ts';

test('50 receipt events produce one snapshot and one follow-up for events arriving during it', async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  let requests = 0;
  let finish!: () => void;
  const scheduler = createLiveSupportRefreshScheduler(() => {
    requests++;
    return new Promise<void>(resolve => { finish = resolve; });
  }, () => assert.fail('snapshot failed'));
  for (let i = 0; i < 50; i++) scheduler.request();
  t.mock.timers.tick(150);
  assert.equal(requests, 1);
  for (let i = 0; i < 50; i++) scheduler.request();
  t.mock.timers.tick(1000);
  assert.equal(requests, 1, 'must not cancel or overlap the pending snapshot');
  finish();
  await new Promise(resolve => setImmediate(resolve));
  t.mock.timers.tick(150);
  assert.equal(requests, 2, 'must reconcile changes received while loading');
  scheduler.dispose();
  finish();
});

test('leaving the view cancels pending refreshes and a failed snapshot can recover', async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  let requests = 0;
  let errors = 0;
  const scheduler = createLiveSupportRefreshScheduler(async () => {
    if (++requests === 1) throw new Error('offline');
  }, () => { errors++; });
  scheduler.request();
  t.mock.timers.tick(150);
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(errors, 1);
  scheduler.request();
  t.mock.timers.tick(150);
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(requests, 2);
  scheduler.request();
  scheduler.dispose();
  t.mock.timers.tick(1000);
  assert.equal(requests, 2);
});

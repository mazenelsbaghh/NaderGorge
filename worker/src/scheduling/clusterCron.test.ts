import { test } from 'node:test';
import assert from 'node:assert/strict';
import { tryRunClusterCron, type ClusterCronOptions } from './clusterCron.js';

function options(task: ClusterCronOptions['task']): ClusterCronOptions {
  return {
    leaseName: 'nightly-test',
    ownerToken: '00000000-0000-0000-0000-000000000001',
    leaseLifetimeMs: 60_000,
    delayUntilNextRun: () => 1,
    task,
  };
}

test('one lease owner executes the scheduled effect', async () => {
  let executions = 0;
  const database = {
    query: async (sql: string) => {
      if (sql.includes('RETURNING')) {
        return { rowCount: 1, rows: [{ FencingGeneration: '4' }] };
      }
      return { rowCount: 1, rows: [] };
    },
  };

  const executed = await tryRunClusterCron(
    database as never,
    options(async () => { executions += 1; }),
  );

  assert.equal(executed, true);
  assert.equal(executions, 1);
});

test('replica without the lease does not execute the scheduled effect', async () => {
  let executions = 0;
  const database = {
    query: async () => ({ rowCount: 0, rows: [] }),
  };

  const executed = await tryRunClusterCron(
    database as never,
    options(async () => { executions += 1; }),
  );

  assert.equal(executed, false);
  assert.equal(executions, 0);
});

test('heartbeat renews a long task and records the fenced completion', async () => {
  let renewals = 0;
  let resolveSecondRenewal: (() => void) | undefined;
  const secondRenewal = new Promise<void>((resolve) => {
    resolveSecondRenewal = resolve;
  });
  const database = {
    query: async (sql: string) => {
      if (sql.includes('RETURNING')) {
        return { rowCount: 1, rows: [{ FencingGeneration: '7' }] };
      }
      if (sql.includes("'running'")) {
        renewals += 1;
        if (renewals === 2) {
          resolveSecondRenewal?.();
        }
      }
      return { rowCount: 1, rows: [] };
    },
  };
  const configured = options(async ({ signal, fencingGeneration }) => {
    assert.equal(fencingGeneration, '7');
    await Promise.race([
      secondRenewal,
      new Promise<never>((_, reject) => setTimeout(
        () => reject(new Error('Timed out waiting for two lease heartbeats.')),
        1_000,
      )),
    ]);
    assert.equal(signal.aborted, false);
  });
  configured.heartbeatIntervalMs = 10;

  assert.equal(await tryRunClusterCron(database as never, configured), true);
  assert.ok(renewals >= 2);
});

test('lost heartbeat aborts the task and refuses a stale completion', async () => {
  let runningUpdate = 0;
  const database = {
    query: async (sql: string) => {
      if (sql.includes('RETURNING')) {
        return { rowCount: 1, rows: [{ FencingGeneration: '8' }] };
      }
      if (sql.includes("'running'")) {
        runningUpdate += 1;
        return { rowCount: 0, rows: [] };
      }
      return { rowCount: 0, rows: [] };
    },
  };
  const configured = options(async ({ signal }) => {
    await new Promise<void>((resolve, reject) => {
      signal.addEventListener('abort', () => reject(signal.reason), { once: true });
      setTimeout(resolve, 100);
    });
  });
  configured.heartbeatIntervalMs = 10;

  await assert.rejects(
    tryRunClusterCron(database as never, configured),
    /lease fencing generation was lost/i,
  );
  assert.equal(runningUpdate, 1);
});

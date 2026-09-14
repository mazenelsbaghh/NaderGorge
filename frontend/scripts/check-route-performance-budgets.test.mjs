import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import { evaluateRoutePerformanceBudgets } from './check-route-performance-budgets.mjs';

const routePaths = ['/login', '/register', '/student', '/admin/ai-agent'];

const adminAIPerformanceContract = {
  route: {
    maximumWarmNavigationP75Ms: 300,
    maximumDuplicateEligibleReads: 0,
  },
  worker: {
    concurrency: 4,
    maximumQueueAgeMs: 300_000,
    ordinaryProviderDeadlineMs: 30_000,
  },
  requestsPerMinute: {
    turnAdmissionsPerAdmin: 10,
    confirmationsPerAdmin: 20,
    secureInputsPerAdmin: 10,
    internalCallbacksPerSourceIp: 120,
  },
  query: {
    maximumModelSteps: 3,
    maximumReadCallsPerTurn: 6,
    maximumReadCallsPerStep: 4,
    maximumRedactedContextBytes: 65_536,
    maximumRecordsPerInvocation: 200,
    maximumQueryTimeoutMs: 5_000,
  },
};

const budgets = {
  routes: Object.fromEntries(
    routePaths.map((pathname) => [
      pathname,
      {
        minimumInitialReductionFromBaseline: 0.25,
        maximumSharedIncreaseFromBaseline: 0,
        maximumDeferredBrotliBytes: 100,
        maximumDuplicateEligibleReads: 0,
        maximumWarmNavigationP75Ms: 300,
      },
    ]),
  ),
  workflows: {
    'live-support-admin': {
      maximumDatabaseCommands: 12,
    },
  },
};

function routeReport({
  initialBrotliBytes,
  sharedBrotliBytes = 500,
  deferredBrotliBytes = 100,
  duplicateEligibleReads = 0,
  warmNavigationP75Ms = 250,
  databaseCommands = 12,
}) {
  return {
    routes: Object.fromEntries(
      routePaths.map((pathname) => [
        pathname.slice(1),
        {
          pathname,
          initial: { bytes: 1, brotliBytes: initialBrotliBytes },
          shared: { bytes: 1, brotliBytes: sharedBrotliBytes },
          deferred: { bytes: 1, brotliBytes: deferredBrotliBytes },
          requests: { duplicateEligibleReads },
          navigation: { warmP75Ms: warmNavigationP75Ms },
        },
      ]),
    ),
    workflows: {
      'live-support-admin': {
        maximumDatabaseCommandsObserved: databaseCommands,
      },
    },
  };
}

test('required public, student, and AdminAI routes pass within compressed budgets', () => {
  const evaluation = evaluateRoutePerformanceBudgets({
    budgets,
    baseline: routeReport({ initialBrotliBytes: 1_000 }),
    candidate: routeReport({ initialBrotliBytes: 750 }),
  });

  assert.equal(evaluation.passed, true);
  assert.deepEqual(
    evaluation.routes.map((route) => route.pathname),
    routePaths,
  );
});

test('AdminAI route, worker, request, and query ceilings match the reviewed protocol', () => {
  assert.deepEqual(adminAIPerformanceContract, {
    route: { maximumWarmNavigationP75Ms: 300, maximumDuplicateEligibleReads: 0 },
    worker: { concurrency: 4, maximumQueueAgeMs: 300_000, ordinaryProviderDeadlineMs: 30_000 },
    requestsPerMinute: { turnAdmissionsPerAdmin: 10, confirmationsPerAdmin: 20, secureInputsPerAdmin: 10, internalCallbacksPerSourceIp: 120 },
    query: { maximumModelSteps: 3, maximumReadCallsPerTurn: 6, maximumReadCallsPerStep: 4, maximumRedactedContextBytes: 65_536, maximumRecordsPerInvocation: 200, maximumQueryTimeoutMs: 5_000 },
  });
  assert.equal(budgets.routes['/admin/ai-agent'].maximumWarmNavigationP75Ms, adminAIPerformanceContract.route.maximumWarmNavigationP75Ms);
  assert.equal(budgets.routes['/admin/ai-agent'].maximumDuplicateEligibleReads, adminAIPerformanceContract.route.maximumDuplicateEligibleReads);
});

test('compressed initial, shared, deferred, and request breaches fail the route gate', () => {
  const evaluation = evaluateRoutePerformanceBudgets({
    budgets,
    baseline: routeReport({ initialBrotliBytes: 1_000 }),
    candidate: routeReport({
      initialBrotliBytes: 751,
      sharedBrotliBytes: 501,
      deferredBrotliBytes: 101,
      duplicateEligibleReads: 1,
      warmNavigationP75Ms: 301,
      databaseCommands: 13,
    }),
  });

  assert.equal(evaluation.passed, false);
  for (const metric of [
    'initial brotli bytes',
    'shared brotli bytes',
    'deferred brotli bytes',
    'duplicate eligible reads',
    'warm navigation p75 ms',
    'database commands',
  ]) {
    assert.ok(
      evaluation.violations.some((violation) => violation.includes(metric)),
      `expected a ${metric} violation`,
    );
  }
});

test('missing budget, compressed, or request evidence fails closed', () => {
  const candidate = routeReport({ initialBrotliBytes: 750 });
  delete candidate.routes.login.shared;
  delete candidate.routes.login.requests;
  delete candidate.routes.login.navigation;
  delete candidate.routes.register.deferred;
  delete candidate.workflows['live-support-admin'];
  const incompleteBudgets = {
    routes: { ...budgets.routes },
    workflows: budgets.workflows,
  };
  delete incompleteBudgets.routes['/student'];

  const evaluation = evaluateRoutePerformanceBudgets({
    budgets: incompleteBudgets,
    baseline: routeReport({ initialBrotliBytes: 1_000 }),
    candidate,
  });

  assert.equal(evaluation.passed, false);
  assert.ok(evaluation.violations.includes('/student: missing route budget'));
  assert.ok(evaluation.violations.includes('/login: missing shared.brotliBytes'));
  assert.ok(
    evaluation.violations.includes('/login: missing requests.duplicateEligibleReads'),
  );
  assert.ok(evaluation.violations.includes('/register: missing deferred.brotliBytes'));
  assert.ok(evaluation.violations.includes('/login: missing navigation.warmP75Ms'));
  assert.ok(
    evaluation.violations.includes(
      'live-support-admin: missing maximumDatabaseCommandsObserved',
    ),
  );
});

test('CLI reads explicit budget, baseline, and candidate artifacts', () => {
  const fixtureDirectory = fs.mkdtempSync(
    path.join(os.tmpdir(), 'massar-route-budget-'),
  );
  const fixturePaths = {
    budgets: path.join(fixtureDirectory, 'budgets.json'),
    baseline: path.join(fixtureDirectory, 'baseline.json'),
    candidate: path.join(fixtureDirectory, 'candidate.json'),
  };

  try {
    fs.writeFileSync(fixturePaths.budgets, JSON.stringify(budgets));
    fs.writeFileSync(
      fixturePaths.baseline,
      JSON.stringify(routeReport({ initialBrotliBytes: 1_000 })),
    );
    fs.writeFileSync(
      fixturePaths.candidate,
      JSON.stringify(routeReport({ initialBrotliBytes: 750 })),
    );

    const execution = spawnSync(
      process.execPath,
      [
        fileURLToPath(new URL('./check-route-performance-budgets.mjs', import.meta.url)),
        '--budgets',
        fixturePaths.budgets,
        '--baseline',
        fixturePaths.baseline,
        '--candidate',
        fixturePaths.candidate,
      ],
      { encoding: 'utf8' },
    );

    assert.equal(execution.status, 0, execution.stderr);
    assert.equal(JSON.parse(execution.stdout).passed, true);
  } finally {
    fs.rmSync(fixtureDirectory, { recursive: true, force: true });
  }
});

import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import { evaluateRoutePerformanceBudgets } from './check-route-performance-budgets.mjs';

const routePaths = ['/login', '/register', '/student'];

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

test('login, register, and student pass within compressed route budgets', () => {
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

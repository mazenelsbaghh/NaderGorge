import fs from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath, pathToFileURL } from 'node:url';

const requiredRoutePaths = ['/login', '/register', '/student'];

function routeForPath(report, pathname) {
  return Object.values(report.routes ?? {}).find(
    (route) => route?.pathname === pathname,
  );
}

function compressedBytes(route, bucket, pathname, violations) {
  const bytes = route?.[bucket]?.brotliBytes;
  if (!Number.isFinite(bytes) || bytes < 0) {
    violations.push(`${pathname}: missing ${bucket}.brotliBytes`);
    return null;
  }
  return bytes;
}

function checkMaximum(pathname, metric, actual, maximum, violations) {
  if (!Number.isFinite(maximum)) {
    violations.push(`${pathname}: missing ${metric} budget`);
  } else if (actual !== null && actual > maximum) {
    violations.push(`${pathname}: ${metric} ${actual} exceeds ${maximum}`);
  }
}

function compressedRouteMetrics(route, pathname, violations) {
  return {
    initial: compressedBytes(route, 'initial', pathname, violations),
    shared: compressedBytes(route, 'shared', pathname, violations),
    deferred: compressedBytes(route, 'deferred', pathname, violations),
  };
}

function maximumCompressedMetrics(routeBudget, baselineMetrics) {
  return {
    initial:
      baselineMetrics.initial *
      (1 - routeBudget.minimumInitialReductionFromBaseline),
    shared:
      baselineMetrics.shared *
      (1 + routeBudget.maximumSharedIncreaseFromBaseline),
    deferred: routeBudget.maximumDeferredBrotliBytes,
  };
}

function checkCompressedBudgets(pathname, candidateMetrics, maximums, violations) {
  for (const bucket of ['initial', 'shared', 'deferred']) {
    checkMaximum(
      pathname,
      `${bucket} brotli bytes`,
      candidateMetrics[bucket],
      maximums[bucket],
      violations,
    );
  }
}

function checkDuplicateReadBudget(pathname, routeBudget, candidateRoute, violations) {
  const duplicateReads = candidateRoute?.requests?.duplicateEligibleReads;
  if (!Number.isInteger(duplicateReads) || duplicateReads < 0) {
    violations.push(`${pathname}: missing requests.duplicateEligibleReads`);
    return;
  }
  checkMaximum(
    pathname,
    'duplicate eligible reads',
    duplicateReads,
    routeBudget.maximumDuplicateEligibleReads,
    violations,
  );
}

function checkNavigationBudget(pathname, routeBudget, candidateRoute, violations) {
  if (!Number.isFinite(routeBudget.maximumWarmNavigationP75Ms)) return;
  const warmP75Ms = candidateRoute?.navigation?.warmP75Ms;
  if (!Number.isFinite(warmP75Ms) || warmP75Ms < 0) {
    violations.push(`${pathname}: missing navigation.warmP75Ms`);
    return;
  }
  checkMaximum(
    pathname,
    'warm navigation p75 ms',
    warmP75Ms,
    routeBudget.maximumWarmNavigationP75Ms,
    violations,
  );
}

function evaluateWorkflowBudgets(budgets, candidate) {
  const violations = [];
  const liveSupportBudget = budgets.workflows?.['live-support-admin'];
  if (liveSupportBudget) {
    const commands =
      candidate.workflows?.['live-support-admin']?.maximumDatabaseCommandsObserved;
    if (!Number.isInteger(commands) || commands < 0) {
      violations.push(
        'live-support-admin: missing maximumDatabaseCommandsObserved',
      );
    } else {
      checkMaximum(
        'live-support-admin',
        'database commands',
        commands,
        liveSupportBudget.maximumDatabaseCommands,
        violations,
      );
    }
  }
  return {
    passed: violations.length === 0,
    violations,
  };
}

function evaluateRoute(pathname, routeBudget, baselineRoute, candidateRoute) {
  const violations = [];
  if (!baselineRoute) violations.push(`${pathname}: missing baseline route`);
  if (!candidateRoute) violations.push(`${pathname}: missing candidate route`);
  const baselineMetrics = compressedRouteMetrics(baselineRoute, pathname, violations);
  const candidateMetrics = compressedRouteMetrics(candidateRoute, pathname, violations);
  const maximums = maximumCompressedMetrics(routeBudget, baselineMetrics);
  checkCompressedBudgets(pathname, candidateMetrics, maximums, violations);
  checkDuplicateReadBudget(pathname, routeBudget, candidateRoute, violations);
  checkNavigationBudget(pathname, routeBudget, candidateRoute, violations);

  return { pathname, passed: violations.length === 0, violations };
}

export function evaluateRoutePerformanceBudgets({ budgets, baseline, candidate }) {
  const missingBudgetViolations = requiredRoutePaths
    .filter((pathname) => !budgets.routes?.[pathname])
    .map((pathname) => `${pathname}: missing route budget`);
  const routeEvaluations = Object.entries(budgets.routes ?? {}).map(
    ([pathname, routeBudget]) =>
      evaluateRoute(
        pathname,
        routeBudget,
        routeForPath(baseline, pathname),
        routeForPath(candidate, pathname),
      ),
  );
  const workflowEvaluation = evaluateWorkflowBudgets(budgets, candidate);
  const violations = [
    ...missingBudgetViolations,
    ...routeEvaluations.flatMap((route) => route.violations),
    ...workflowEvaluation.violations,
  ];

  return {
    passed: violations.length === 0,
    violations,
    routes: routeEvaluations,
    workflows: workflowEvaluation,
  };
}

function readJson(jsonPath) {
  return JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
}

function cliPaths() {
  const frontendRoot = fileURLToPath(new URL('..', import.meta.url));
  const repositoryRoot = path.resolve(frontendRoot, '..');
  const { values } = parseArgs({
    options: {
      budgets: { type: 'string' },
      baseline: { type: 'string' },
      candidate: { type: 'string' },
    },
  });

  return {
    budgets: path.resolve(values.budgets ?? path.join(frontendRoot, 'performance-budgets.json')),
    baseline: path.resolve(
      values.baseline ??
        path.join(repositoryRoot, 'artifacts/performance-167/baseline/frontend-routes.json'),
    ),
    candidate: path.resolve(
      values.candidate ??
        path.join(repositoryRoot, 'artifacts/performance-167/final/frontend-routes.json'),
    ),
  };
}

function runCli() {
  const paths = cliPaths();
  const evaluation = evaluateRoutePerformanceBudgets({
    budgets: readJson(paths.budgets),
    baseline: readJson(paths.baseline),
    candidate: readJson(paths.candidate),
  });

  console.log(JSON.stringify({ ...evaluation, inputs: paths }, null, 2));
  if (!evaluation.passed) process.exitCode = 1;
}

if (
  process.argv[1] &&
  import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href
) {
  runCli();
}

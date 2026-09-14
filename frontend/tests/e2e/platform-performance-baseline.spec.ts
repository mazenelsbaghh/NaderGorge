import fs from 'node:fs';
import { performance } from 'node:perf_hooks';
import path from 'node:path';

import {
  devices,
  errors,
  expect,
  test,
  type Page,
  type Request,
} from '@playwright/test';

import {
  readPerformanceSourceBinding,
  resolveRawEvidenceOutput,
  writeJsonEvidenceCreateNew,
} from '../../scripts/performance-evidence-io.mjs';
import {
  aggregateEligibleReads,
  eligibleReadIdentity,
  nearestRankP75,
  type EligibleReadCount,
  type EligibleReadIdentity,
  type EligibleReadOrigins,
} from './platform-performance-evidence';
import { installAuthAndGoto, login, seedE2E } from './e2e-contract-helpers';

const WARMUP_COUNT = 3;
const MEASURED_COUNT = 20;
const QUIET_WINDOW_MS = 250;
const QUIET_TIMEOUT_MS = 2_000;
const androidProfile = devices['Pixel 5'];

test.use({
  ...androidProfile,
  trace: 'off',
  screenshot: 'off',
  video: 'off',
});
test.describe.configure({ mode: 'serial', retries: 0 });

type RouteName = 'login' | 'register' | 'student';

type BrowserSample = {
  sequence: number;
  warmNavigationMs: number;
  eligibleReads: EligibleReadCount[];
};

type RouteScenario = {
  name: RouteName;
  pathname: `/${string}`;
  prepare: (page: Page) => Promise<void>;
  navigate: (page: Page) => Promise<void>;
};

function repositoryRoot() {
  return path.basename(process.cwd()) === 'frontend'
    ? path.resolve(process.cwd(), '..')
    : process.cwd();
}

function startEligibleReadCapture(page: Page, allowedOrigins: EligibleReadOrigins) {
  const identities: EligibleReadIdentity[] = [];
  const trackedRequests = new WeakSet<Request>();
  let captureFailure: Error | null = null;
  let quietStartedAt = 0;
  let lastActivityAt = performance.now();

  const recordActivity = () => {
    lastActivityAt = performance.now();
  };
  const onRequest = (request: Request) => {
    let identity: EligibleReadIdentity | null;
    try {
      identity = eligibleReadIdentity({
        method: request.method(),
        resourceType: request.resourceType(),
        url: request.url(),
        headers: request.headers(),
      }, allowedOrigins);
    } catch (error) {
      captureFailure = error instanceof Error
        ? error
        : new Error('Eligible-read classification failed.');
      recordActivity();
      return;
    }
    if (!identity) return;
    trackedRequests.add(request);
    identities.push(identity);
    recordActivity();
  };
  const onRequestComplete = (request: Request) => {
    if (trackedRequests.has(request)) recordActivity();
  };

  page.on('request', onRequest);
  page.on('requestfinished', onRequestComplete);
  page.on('requestfailed', onRequestComplete);

  const stop = () => {
    page.off('request', onRequest);
    page.off('requestfinished', onRequestComplete);
    page.off('requestfailed', onRequestComplete);
  };

  return {
    beginQuietWindow() {
      quietStartedAt = performance.now();
      lastActivityAt = quietStartedAt;
    },
    async finish() {
      if (quietStartedAt === 0) {
        throw new Error('The eligible-read quiet window was not started.');
      }
      try {
        for (;;) {
          if (captureFailure) throw captureFailure;
          const now = performance.now();
          if (now - lastActivityAt >= QUIET_WINDOW_MS) {
            return aggregateEligibleReads(identities);
          }
          if (now - quietStartedAt >= QUIET_TIMEOUT_MS) {
            throw new Error(
              `Eligible reads did not become quiet within ${QUIET_TIMEOUT_MS}ms.`,
            );
          }
          await page.waitForTimeout(
            Math.max(1, Math.min(50, QUIET_WINDOW_MS - (now - lastActivityAt))),
          );
        }
      } finally {
        stop();
      }
    },
    stop,
  };
}

async function settleEligibleReads(page: Page, allowedOrigins: EligibleReadOrigins) {
  const capture = startEligibleReadCapture(page, allowedOrigins);
  capture.beginQuietWindow();
  await capture.finish();
}

async function dismissInstructions(page: Page) {
  const dialog = page.getByRole('dialog').first();
  try {
    await dialog.waitFor({ state: 'visible', timeout: 1_000 });
  } catch (error) {
    if (error instanceof errors.TimeoutError) return;
    throw error;
  }
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
}

async function dismissParentTrackingPopup(page: Page) {
  const dialog = page.getByRole('dialog', {
    name: 'تابع مستواك الدراسي مع ولي أمرك',
  });
  try {
    await dialog.waitFor({ state: 'visible', timeout: 5_000 });
  } catch (error) {
    if (error instanceof errors.TimeoutError) return;
    throw error;
  }

  await dialog.getByRole('button', { name: 'حفظ ومتابعة' }).click();
  await expect(dialog).toBeHidden({ timeout: 15_000 });
}

async function measureScenario(
  page: Page,
  scenario: RouteScenario,
  allowedOrigins: EligibleReadOrigins,
) {
  await scenario.prepare(page);
  await settleEligibleReads(page, allowedOrigins);

  const capture = startEligibleReadCapture(page, allowedOrigins);
  const startedAt = performance.now();
  try {
    await scenario.navigate(page);
    const warmNavigationMs = performance.now() - startedAt;
    capture.beginQuietWindow();
    const eligibleReads = await capture.finish();
    return { warmNavigationMs, eligibleReads };
  } catch (error) {
    capture.stop();
    throw error;
  }
}

async function collectRouteSamples(
  page: Page,
  scenario: RouteScenario,
  allowedOrigins: EligibleReadOrigins,
) {
  const samples: BrowserSample[] = [];
  for (let run = 0; run < WARMUP_COUNT + MEASURED_COUNT; run += 1) {
    const measurement = await measureScenario(page, scenario, allowedOrigins);
    if (run >= WARMUP_COUNT) {
      samples.push({
        sequence: run - WARMUP_COUNT + 1,
        warmNavigationMs: measurement.warmNavigationMs,
        eligibleReads: measurement.eligibleReads,
      });
    }
  }

  expect(samples).toHaveLength(MEASURED_COUNT);
  expect(
    nearestRankP75(samples.map((sample) => sample.warmNavigationMs)),
  ).toBeGreaterThanOrEqual(0);
  return samples;
}

test.describe('Platform performance 167 raw browser producer', () => {
  test('records bounded privacy-safe warm navigation samples', async ({
    browserName,
    page,
    request,
  }, testInfo) => {
    test.setTimeout(12 * 60_000);
    expect(browserName).toBe('chromium');
    expect(process.env.PLAYWRIGHT_USE_PRODUCTION_BUILD).toBe('1');

    const projectRoot = repositoryRoot();
    const webPort = process.env.PLAYWRIGHT_WEB_PORT || '3000';
    const appOrigin = process.env.PLAYWRIGHT_BASE_URL || `http://app.lvh.me:${webPort}`;
    const apiOrigin = new URL(
      process.env.E2E_API_URL || 'http://api.lvh.me:5245/api',
    ).origin;
    const allowedOrigins = { appOrigin, apiOrigin };
    const outputPath = resolveRawEvidenceOutput(
      projectRoot,
      process.env.PERFORMANCE_BROWSER_OUTPUT,
      'browser-samples.json',
    );
    const source = readPerformanceSourceBinding({
      repositoryRoot: projectRoot,
      manifestPath: process.env.PERFORMANCE_SOURCE_MANIFEST,
    });
    const buildIdPath = path.join(projectRoot, 'frontend/.next/BUILD_ID');
    const buildIdStat = fs.lstatSync(buildIdPath);
    expect(buildIdStat.isSymbolicLink()).toBe(false);
    expect(buildIdStat.isFile()).toBe(true);
    const buildId = fs.readFileSync(buildIdPath, 'utf8').trim();

    await seedE2E(request, 'Performance samples require the documented E2E seed.');
    const studentSession = await login(request, 'student');
    await page.addInitScript(
      ({ studentId }) => {
        window.localStorage.setItem(`onboarding_ack_${studentId}`, '1');
      },
      { studentId: studentSession.user.id },
    );
    let studentPreparationCompleted = false;

    const scenarios: RouteScenario[] = [
      {
        name: 'login',
        pathname: '/login',
        prepare: async (targetPage) => {
          await targetPage.goto(`${appOrigin}/register`, {
            waitUntil: 'domcontentloaded',
          });
          await expect(targetPage).toHaveURL(/\/register$/);
          await dismissInstructions(targetPage);
        },
        navigate: async (targetPage) => {
          await targetPage.locator('a[href="/login"]:visible').first().click();
          await expect(targetPage).toHaveURL(/\/login$/);
          await expect(targetPage.getByLabel('رقم الهاتف')).toBeVisible();
        },
      },
      {
        name: 'register',
        pathname: '/register',
        prepare: async (targetPage) => {
          await targetPage.goto(`${appOrigin}/login`, {
            waitUntil: 'domcontentloaded',
          });
          await expect(targetPage).toHaveURL(/\/login$/);
          await dismissInstructions(targetPage);
        },
        navigate: async (targetPage) => {
          await targetPage.locator('a[href="/register"]:visible').first().click();
          await expect(targetPage).toHaveURL(/\/register$/);
          await expect(
            targetPage.getByRole('heading', {
              level: 1,
              name: 'افتح حسابك خطوة بخطوة',
            }),
          ).toBeVisible();
        },
      },
      {
        name: 'student',
        pathname: '/student',
        prepare: async (targetPage) => {
          await installAuthAndGoto(
            targetPage,
            studentSession.accessToken,
            studentSession.user,
            `${appOrigin}/student/packages`,
          );
          await expect(targetPage).toHaveURL(/\/student\/packages$/);
          await expect(
            targetPage.getByRole('heading', {
              level: 1,
              name: 'الباقات والمسارات',
            }),
          ).toBeVisible({ timeout: 15_000 });
          if (!studentPreparationCompleted) {
            await dismissParentTrackingPopup(targetPage);
            studentPreparationCompleted = true;
          }
        },
        navigate: async (targetPage) => {
          await targetPage.locator('a[href="/student"]:visible').first().click();
          await expect(targetPage).toHaveURL(/\/student$/);
          await expect(
            targetPage.getByRole('heading', {
              level: 1,
              name: /أهلاً بيك،/,
            }),
          ).toBeVisible({ timeout: 15_000 });
        },
      },
    ];

    const routes = {} as Record<
      RouteName,
      { pathname: `/${string}`; samples: BrowserSample[] }
    >;
    for (const scenario of scenarios) {
      routes[scenario.name] = {
        pathname: scenario.pathname,
        samples: await collectRouteSamples(page, scenario, allowedOrigins),
      };
    }

    const viewport = page.viewportSize();
    expect(viewport).toEqual(androidProfile.viewport);
    const evidence = {
      schemaVersion: 1,
      evidenceType: 'browser-performance-samples',
      source,
      profile: {
        name: 'Pixel 5 / Android Chromium',
        browserName: 'chromium' as const,
        viewport,
        productionServer: true,
        buildId,
      },
      sampling: {
        warmupCount: WARMUP_COUNT,
        measuredCount: MEASURED_COUNT,
        quietWindowMs: QUIET_WINDOW_MS,
        quietTimeoutMs: QUIET_TIMEOUT_MS,
        percentileMethod: 'nearest-rank' as const,
      },
      routes,
    };

    const serializedEvidence = JSON.stringify(evidence);
    expect(serializedEvidence).not.toMatch(/https?:\/\//i);
    expect(serializedEvidence).not.toContain(appOrigin);
    writeJsonEvidenceCreateNew(outputPath, evidence);
    await testInfo.attach('platform-performance-167-browser-samples', {
      body: JSON.stringify(evidence, null, 2),
      contentType: 'application/json',
    });
  });
});

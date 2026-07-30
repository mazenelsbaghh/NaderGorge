import fs from 'node:fs';
import path from 'node:path';

import { devices, expect, test, type Page, type Request } from '@playwright/test';

import {
  installAuthAndGoto,
  login,
  seedE2E,
} from './e2e-contract-helpers';

const androidProfile = devices['Pixel 5'];
test.use({
  ...androidProfile,
});

type RequestSummary = {
  total: number;
  get: number;
  api: number;
  nextData: number;
  byPath: Record<string, number>;
};

type LongTaskEntry = {
  startTime: number;
  duration: number;
};

declare global {
  interface Window {
    __performanceBaselineLongTasks?: LongTaskEntry[];
  }
}

function repositoryRoot() {
  return path.basename(process.cwd()) === 'frontend'
    ? path.resolve(process.cwd(), '..')
    : process.cwd();
}

function normalizedRequestPath(request: Request) {
  const url = new URL(request.url());
  return url.pathname
    .replace(
      /\/[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}(?=\/|$)/gi,
      '/:id'
    )
    .replace(/\/\d+(?=\/|$)/g, '/:number');
}

function startRequestCapture(page: Page) {
  const requests: Request[] = [];
  const listener = (request: Request) => {
    if (request.resourceType() === 'websocket') return;
    requests.push(request);
  };
  page.on('request', listener);

  return () => {
    page.off('request', listener);
    const byPath: Record<string, number> = {};
    for (const request of requests) {
      const key = normalizedRequestPath(request);
      byPath[key] = (byPath[key] ?? 0) + 1;
    }
    return {
      total: requests.length,
      get: requests.filter((request) => request.method() === 'GET').length,
      api: requests.filter((request) => new URL(request.url()).pathname.startsWith('/api/')).length,
      nextData: requests.filter((request) => {
        const url = new URL(request.url());
        return url.pathname.startsWith('/_next/') || url.searchParams.has('_rsc');
      }).length,
      byPath: Object.fromEntries(
        Object.entries(byPath).sort(([left], [right]) => left.localeCompare(right))
      ),
    } satisfies RequestSummary;
  };
}

async function installLongTaskObserver(page: Page) {
  await page.addInitScript(() => {
    window.__performanceBaselineLongTasks = [];
    if (!('PerformanceObserver' in window)) return;
    try {
      const observer = new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) {
          window.__performanceBaselineLongTasks?.push({
            startTime: entry.startTime,
            duration: entry.duration,
          });
        }
      });
      observer.observe({ type: 'longtask', buffered: true });
    } catch {
      // The browser may not expose Long Tasks; the baseline records that as unsupported.
    }
  });
}

async function measureInputResponse(page: Page, selector: string, value: string) {
  return page.locator(selector).evaluate(async (element, nextValue) => {
    const input = element as HTMLInputElement;
    const startedAt = performance.now();
    input.focus();
    input.value = nextValue;
    input.dispatchEvent(
      new InputEvent('input', {
        bubbles: true,
        data: nextValue,
        inputType: 'insertText',
      })
    );
    await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
    return performance.now() - startedAt;
  }, value);
}

async function navigationTiming(page: Page) {
  return page.evaluate(() => {
    const navigation = performance.getEntriesByType(
      'navigation'
    )[0] as PerformanceNavigationTiming | undefined;
    return navigation
      ? {
          domContentLoadedMs:
            navigation.domContentLoadedEventEnd - navigation.startTime,
          loadMs: navigation.loadEventEnd - navigation.startTime,
          responseStartMs: navigation.responseStart - navigation.startTime,
        }
      : null;
  });
}

async function longTaskSummary(page: Page) {
  return page.evaluate(() => {
    const entries = window.__performanceBaselineLongTasks;
    if (!entries) return { supported: false, count: 0, maxDurationMs: null };
    return {
      supported: true,
      count: entries.length,
      maxDurationMs:
        entries.length > 0
          ? Math.max(...entries.map((entry) => entry.duration))
          : 0,
    };
  });
}

test.describe('Platform performance 167 baseline', () => {
  test('records public entry, student navigation, request counts, and Android interaction', async ({
    browserName,
    page,
    request,
  }, testInfo) => {
    test.skip(browserName !== 'chromium', 'The fixed Android baseline uses Chromium.');

    const webPort = process.env.PLAYWRIGHT_WEB_PORT || '3000';
    const appOrigin =
      process.env.PLAYWRIGHT_BASE_URL || `http://app.lvh.me:${webPort}`;
    const outputPath = path.join(
      repositoryRoot(),
      'artifacts/performance-167/baseline/browser-baseline.json'
    );

    await installLongTaskObserver(page);
    await seedE2E(request, 'Performance baseline requires the documented E2E seed.');

    const loginRequests = startRequestCapture(page);
    await page.goto(`${appOrigin}/login`, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('input[type="tel"]').first()).toBeVisible();
    const loginInputResponseMs = await measureInputResponse(
      page,
      'input[type="tel"]',
      '20000000001'
    );
    const loginEvidence = {
      navigation: await navigationTiming(page),
      inputResponseMs: loginInputResponseMs,
      requests: loginRequests(),
      longTasks: await longTaskSummary(page),
    };

    const registerRequests = startRequestCapture(page);
    const registerStartedAt = Date.now();
    await page.getByRole('link', { name: /إنشاء حساب|حساب جديد/ }).first().click();
    await expect(page).toHaveURL(/\/register$/);
    await expect(page.locator('main h1')).toBeVisible();
    const registerUsableMs = Date.now() - registerStartedAt;
    const registerInput = page.locator('input').first();
    const registerInputResponseMs = await measureInputResponse(
      page,
      `#${await registerInput.getAttribute('id')}`,
      'طالب قياس الأداء'
    ).catch(() => null);
    const registerEvidence = {
      clientNavigationUsableMs: registerUsableMs,
      inputResponseMs: registerInputResponseMs,
      requests: registerRequests(),
      longTasks: await longTaskSummary(page),
    };

    const studentSession = await login(request, 'student');
    const studentInitialRequests = startRequestCapture(page);
    await installAuthAndGoto(
      page,
      studentSession.accessToken,
      studentSession.user,
      `${appOrigin}/student`
    );
    await expect(page).toHaveURL(/\/student$/);
    await expect(page.getByText('بوابة الطالب').first()).toBeVisible({
      timeout: 15_000,
    });
    const studentInitialEvidence = {
      navigation: await navigationTiming(page),
      requests: studentInitialRequests(),
      longTasks: await longTaskSummary(page),
    };

    const studentNavigationRequests = startRequestCapture(page);
    const studentNavigationStartedAt = Date.now();
    await page.locator('a[href="/student/packages"]').first().click();
    await expect(page).toHaveURL(/\/student\/packages$/);
    await expect(page.getByText('باقاتي').first()).toBeVisible({
      timeout: 15_000,
    });
    const studentNavigationEvidence = {
      clientNavigationUsableMs: Date.now() - studentNavigationStartedAt,
      requests: studentNavigationRequests(),
      longTasks: await longTaskSummary(page),
    };

    const evidence = {
      schemaVersion: 1,
      generatedAt: new Date().toISOString(),
      profile: {
        name: 'Pixel 5 / Android Chromium',
        browserName,
        viewport: page.viewportSize(),
        productionServer:
          process.env.PLAYWRIGHT_USE_PRODUCTION_BUILD === '1',
      },
      measurements: {
        login: loginEvidence,
        register: registerEvidence,
        studentInitial: studentInitialEvidence,
        studentNavigation: studentNavigationEvidence,
      },
      note:
        'Observational baseline only. It intentionally defines no pass/fail performance threshold.',
    };

    fs.mkdirSync(path.dirname(outputPath), { recursive: true });
    fs.writeFileSync(outputPath, `${JSON.stringify(evidence, null, 2)}\n`);
    await testInfo.attach('platform-performance-167-browser-baseline', {
      body: JSON.stringify(evidence, null, 2),
      contentType: 'application/json',
    });

    expect(loginEvidence.requests.total).toBeGreaterThan(0);
    expect(registerEvidence.clientNavigationUsableMs).toBeGreaterThanOrEqual(0);
    expect(studentNavigationEvidence.requests.total).toBeGreaterThan(0);
  });
});

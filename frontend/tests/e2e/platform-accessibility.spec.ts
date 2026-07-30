import axe from 'axe-core';
import { expect, test, type Page } from '@playwright/test';

import {
  adminUrl,
  appUrl,
  installAuthAndGoto,
  staffUrl,
} from './e2e-contract-helpers';

type AxeViolation = {
  id: string;
  impact: string | null;
  nodes: Array<{ target: string[] }>;
};

const surfaceUsers = {
  student: {
    id: 'axe-student',
    fullName: 'طالب الوصول',
    phone: '20000000701',
    roles: ['Student'],
    permissions: [],
    profileComplete: true,
    allowedDomains: ['student'],
    allowedNavbarItems: [],
    authorizationVersion: 1,
  },
  assistant: {
    id: 'axe-assistant',
    fullName: 'مساعد الوصول',
    phone: '20000000702',
    roles: ['Assistant'],
    permissions: [],
    profileComplete: true,
    allowedDomains: ['assistant'],
    allowedNavbarItems: [],
    authorizationVersion: 1,
  },
  admin: {
    id: 'axe-admin',
    fullName: 'مدير الوصول',
    phone: '20000000703',
    roles: ['Admin'],
    permissions: ['users.manage'],
    profileComplete: true,
    allowedDomains: ['admin'],
    allowedNavbarItems: [],
    authorizationVersion: 1,
  },
};

async function installSurfaceApi(page: Page, user: object) {
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname;
    const data = pathname.endsWith('/auth/session')
      ? { user, authorizationVersion: 1 }
      : null;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data }),
    });
  });
}

async function criticalViolations(page: Page) {
  await page.addScriptTag({ content: axe.source });
  return page.evaluate(async () => {
    const axeApi = (window as typeof window & {
      axe: {
        run: (
          context: Document,
          options: object,
        ) => Promise<{ violations: AxeViolation[] }>;
      };
    }).axe;
    const scan = await axeApi.run(document, {
      runOnly: {
        type: 'tag',
        values: ['wcag2a', 'wcag2aa', 'wcag21aa'],
      },
    });
    return scan.violations
      .filter((violation) => violation.impact === 'critical')
      .map((violation) => ({
        id: violation.id,
        targets: violation.nodes.flatMap((node) => node.target),
      }));
  });
}

const publicRoutes = [
  { name: 'landing', url: appUrl },
  { name: 'login', url: `${appUrl}/login` },
  { name: 'register', url: `${appUrl}/register` },
];

for (const route of publicRoutes) {
  test(`${route.name} has no critical axe violations`, async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('hasSeenRegisterInstructions', 'true');
    });
    await page.route('**/api/**', (request) =>
      request.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true, data: [] }),
      }),
    );
    await page.goto(route.url);
    await expect(page.locator('body')).toBeVisible();

    expect(await criticalViolations(page)).toEqual([]);
  });
}

const authenticatedRoutes = [
  {
    name: 'student',
    url: `${appUrl}/student`,
    user: surfaceUsers.student,
  },
  {
    name: 'assistant',
    url: `${staffUrl}/assistant/dashboard`,
    user: surfaceUsers.assistant,
  },
  {
    name: 'admin',
    url: `${adminUrl}/admin`,
    user: surfaceUsers.admin,
  },
];

for (const route of authenticatedRoutes) {
  test(`${route.name} surface has no critical axe violations`, async ({ page }) => {
    await installSurfaceApi(page, route.user);
    await installAuthAndGoto(page, `axe-${route.name}-token`, route.user, route.url);
    await expect(page.locator('#main-content')).toBeVisible();

    expect(await criticalViolations(page)).toEqual([]);
  });
}

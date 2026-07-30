import {
  expect,
  test,
  type APIRequestContext,
  type Page,
} from '@playwright/test';

import {
  accounts,
  apiUrl,
  e2eHeaders,
  installAuthAndGoto,
  login,
  seedE2E,
} from './e2e-contract-helpers';

type SurfaceCase = {
  name: 'student' | 'assistant' | 'teacher' | 'admin';
  account?: keyof typeof accounts;
  phoneNumber?: string;
  surface: 'student' | 'assistant' | 'teacher' | 'admin';
  origin: string;
  initialPath: string;
  destinationPath: string;
  shellTestId?: string;
  bootstrapPath?: string;
};

const webPort = process.env.PLAYWRIGHT_WEB_PORT || '3000';
const surfaces: SurfaceCase[] = [
  {
    name: 'student',
    account: 'student',
    surface: 'student',
    origin: process.env.PLAYWRIGHT_BASE_URL || `http://app.lvh.me:${webPort}`,
    initialPath: '/student',
    destinationPath: '/student/packages',
    shellTestId: 'student-shell',
    bootstrapPath: '/api/student/shell-bootstrap',
  },
  {
    name: 'assistant',
    account: 'assistant',
    surface: 'assistant',
    origin: process.env.STAFF_E2E_URL || `http://staff.lvh.me:${webPort}`,
    initialPath: '/assistant/dashboard',
    destinationPath: '/assistant/tasks',
    shellTestId: 'assistant-shell',
  },
  {
    name: 'teacher',
    phoneNumber: '20000000004',
    surface: 'teacher',
    origin: process.env.TEACHER_E2E_URL || `http://teacher.lvh.me:${webPort}`,
    initialPath: '/teacher',
    destinationPath: '/teacher/students',
  },
  {
    name: 'admin',
    account: 'admin',
    surface: 'admin',
    origin: process.env.ADMIN_E2E_URL || `http://admin.lvh.me:${webPort}`,
    initialPath: '/admin',
    destinationPath: '/admin/users',
    shellTestId: 'admin-shell',
  },
];

async function loginSurface(
  request: APIRequestContext,
  surface: SurfaceCase
) {
  if (surface.account) return login(request, surface.account);

  const response = await request.post(`${apiUrl}/auth/login`, {
    headers: {
      ...e2eHeaders,
      'X-App-Surface': surface.surface,
    },
    data: {
      phoneNumber: surface.phoneNumber,
      password: 'password',
      deviceFingerprint: `persistent-shell-${surface.name}-${Date.now()}`,
    },
  });
  expect(response.ok(), `${surface.name} E2E login must succeed`).toBeTruthy();
  const payload = await response.json();
  const body = payload?.data ?? payload;
  expect(body?.accessToken).toBeTruthy();
  return body as {
    accessToken: string;
    user: {
      id: string;
      roles: string[];
      permissions?: string[];
      fullName?: string;
      phone?: string;
      profileComplete?: boolean;
    };
  };
}

async function shellLocator(page: Page, surface: SurfaceCase) {
  if (surface.shellTestId) {
    return page.getByTestId(surface.shellTestId);
  }
  return page.locator('main.app-shell-scroll').first().locator('..');
}

async function ensureShellIdentity(page: Page, surface: SurfaceCase) {
  const shell = await shellLocator(page, surface);
  await expect(shell).toHaveCount(1);
  return shell.evaluate((element) => {
    const htmlElement = element as HTMLElement;
    htmlElement.dataset.shellContractId ||= crypto.randomUUID();
    return htmlElement.dataset.shellContractId;
  });
}

test.describe('US1 persistent protected-surface shells', () => {
  for (const surface of surfaces) {
    test(`${surface.name} keeps shell identity, navigation state, history scroll, focus, and bootstrap bounds`, async ({
      page,
      request,
    }) => {
      await seedE2E(
        request,
        `${surface.name} persistent-shell contract requires the E2E seed.`
      );
      const session = await loginSurface(request, surface);

      let bootstrapRequests = 0;
      if (surface.bootstrapPath) {
        page.on('request', (outgoing) => {
          if (
            outgoing.method() === 'GET' &&
            new URL(outgoing.url()).pathname === surface.bootstrapPath
          ) {
            bootstrapRequests += 1;
          }
        });
      }

      await installAuthAndGoto(
        page,
        session.accessToken,
        session.user,
        `${surface.origin}${surface.initialPath}`
      );
      await expect(page).toHaveURL(
        new RegExp(`${surface.initialPath.replaceAll('/', '\\/')}$`)
      );
      await expect(page.locator('main.app-shell-scroll')).toHaveCount(1);

      const originalShellIdentity = await ensureShellIdentity(page, surface);
      const collapseButton = page.getByRole('button', {
        name: /طي القائمة الجانبية/,
      });
      const canCollapse = await collapseButton
        .isVisible()
        .catch(() => false);
      if (canCollapse) {
        await collapseButton.click();
        await expect(
          page.getByRole('button', { name: /توسيع القائمة الجانبية/ })
        ).toBeVisible();
      }

      const scroller = page.locator('main.app-shell-scroll').first();
      await scroller.evaluate((element) => {
        element.scrollTop = 420;
      });

      await page
        .locator(`a[href="${surface.destinationPath}"]`)
        .first()
        .click();
      await expect(page).toHaveURL(
        new RegExp(`${surface.destinationPath.replaceAll('/', '\\/')}$`)
      );
      await expect(page.locator('main.app-shell-scroll')).toHaveCount(1);
      expect(await ensureShellIdentity(page, surface)).toBe(
        originalShellIdentity
      );

      if (canCollapse) {
        await expect(
          page.getByRole('button', { name: /توسيع القائمة الجانبية/ })
        ).toBeVisible();
      }

      const destinationOwnsFocus = await page.evaluate(() => {
        const main = document.querySelector('main.app-shell-scroll');
        const active = document.activeElement;
        return Boolean(
          main &&
            active &&
            (active === main ||
              main.contains(active) ||
              active.matches('h1, [role="heading"]'))
        );
      });
      expect(
        destinationOwnsFocus,
        'Client navigation must move focus into destination content.'
      ).toBeTruthy();

      await page.goBack();
      await expect(page).toHaveURL(
        new RegExp(`${surface.initialPath.replaceAll('/', '\\/')}$`)
      );
      expect(await ensureShellIdentity(page, surface)).toBe(
        originalShellIdentity
      );
      await expect
        .poll(() => scroller.evaluate((element) => element.scrollTop))
        .toBeGreaterThan(200);

      if (surface.bootstrapPath) {
        expect(
          bootstrapRequests,
          'The persistent student shell must not repeat bootstrap on navigation.'
        ).toBeLessThanOrEqual(1);
      }
    });
  }
});

import { devices, expect, test, type Page } from '@playwright/test';

import { appUrl } from './e2e-contract-helpers';

const androidProfile = devices['Pixel 5'];

test.use({
  ...androidProfile,
});

declare global {
  interface Window {
    __registrationLongTasks?: number[];
  }
}

async function prepareRegistration(page: Page) {
  await page.addInitScript(() => {
    localStorage.setItem('hasSeenRegisterInstructions', 'true');
  });
}

async function expectStaticRegistrationBackground(page: Page) {
  await expect(page.locator('.auth-shell__static-grid')).toBeVisible();
  await expect(page.locator('.auth-shell__static-grid > div')).toHaveCount(0);
  await expect(page.locator('.auth-shell__static-grid canvas')).toHaveCount(0);
}

test.describe('registration performance gates', () => {
  test('constrained Android devices keep the lightweight static background', async ({
    page,
  }) => {
    await page.addInitScript(() => {
      Object.defineProperty(Navigator.prototype, 'deviceMemory', {
        configurable: true,
        get: () => 2,
      });
      Object.defineProperty(Navigator.prototype, 'hardwareConcurrency', {
        configurable: true,
        get: () => 2,
      });
    });
    await prepareRegistration(page);

    await page.goto(`${appUrl}/register`);

    await expect(page.locator('#reg-fullName')).toBeEditable();
    await expectStaticRegistrationBackground(page);
  });

  test('reduced-motion preference never mounts the enhanced background', async ({
    page,
  }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await prepareRegistration(page);

    await page.goto(`${appUrl}/register`);

    await expect(page.locator('#reg-fullName')).toBeEditable();
    await expectStaticRegistrationBackground(page);
  });

  test('a hidden registration tab does not start enhanced motion', async ({
    page,
  }) => {
    await page.addInitScript(() => {
      Object.defineProperty(document, 'visibilityState', {
        configurable: true,
        get: () => 'hidden',
      });
    });
    await prepareRegistration(page);

    await page.goto(`${appUrl}/register`);

    await expect(page.locator('#reg-fullName')).toBeEditable();
    await expectStaticRegistrationBackground(page);
  });

  test('typing remains responsive without interaction long tasks', async ({
    page,
  }) => {
    await page.addInitScript(() => {
      window.__registrationLongTasks = [];
      if (!('PerformanceObserver' in window)) return;

      try {
        const observer = new PerformanceObserver((list) => {
          for (const entry of list.getEntries()) {
            window.__registrationLongTasks?.push(entry.duration);
          }
        });
        observer.observe({ type: 'longtask', buffered: true });
      } catch {
        window.__registrationLongTasks = undefined;
      }
    });
    await prepareRegistration(page);
    await page.goto(`${appUrl}/register`);
    const fullName = page.locator('#reg-fullName');
    await expect(fullName).toBeEditable();

    await page.evaluate(() => {
      if (window.__registrationLongTasks) {
        window.__registrationLongTasks.length = 0;
      }
    });
    await fullName.pressSequentially('أحمد محمد محمود علي', { delay: 10 });
    await expect(fullName).toHaveValue('أحمد محمد محمود علي');
    await page.evaluate(() => new Promise<void>((resolve) => {
      requestAnimationFrame(() => resolve());
    }));

    const longTasks = await page.evaluate(() => window.__registrationLongTasks);
    test.skip(longTasks === undefined, 'Long Tasks API is unavailable in this browser.');
    expect(Math.max(0, ...(longTasks ?? []))).toBeLessThanOrEqual(50);
  });
});

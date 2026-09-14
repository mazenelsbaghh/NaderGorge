import { devices, expect, test, type Page, type Request } from '@playwright/test';

import { appUrl } from './e2e-contract-helpers';

function decodedImagePath(src: string | null) {
  if (!src) return '';
  const url = new URL(src, appUrl);
  return url.searchParams.get('url') ?? url.pathname;
}

async function skipRegistrationInstructions(page: Page) {
  await page.addInitScript(() => {
    localStorage.setItem('hasSeenRegisterInstructions', 'true');
  });
}

test.describe('entry asset contracts', () => {
  test('registration renders one active logo and swaps its theme asset in place', async ({
    page,
  }) => {
    await skipRegistrationInstructions(page);
    await page.goto(`${appUrl}/register`);

    const logo = page.getByRole('img', { name: 'شعار منصة مسار' });
    await expect(logo).toHaveCount(1);
    await expect(logo).toBeVisible();
    await expect(logo).toHaveAttribute('fetchpriority', 'high');
    expect(decodedImagePath(await logo.getAttribute('src'))).toBe('/images/logo-mark.svg');

    await page.getByRole('button', { name: 'التحويل إلى الوضع الداكن' }).click();

    await expect(page.getByRole('img', { name: 'شعار منصة مسار' })).toHaveCount(1);
    await expect(logo).toHaveAttribute('src', /logo-mark-light/);
    await expect(page.locator('img[fetchpriority="high"]:visible')).toHaveCount(1);
  });

  test('mobile landing does not request the desktop hero or hidden priority images', async ({
    browser,
  }) => {
    const context = await browser.newContext({ ...devices['iPhone 13'] });
    const page = await context.newPage();
    const heroRequests: Request[] = [];
    page.on('request', (request) => {
      if (/\/images\/landing-hero(?:-dark)?\.webp$/.test(new URL(request.url()).pathname)) {
        heroRequests.push(request);
      }
    });

    await page.goto(appUrl);
    await expect(page.getByRole('heading', { name: /ابدأ رحلتك التعليمية/ })).toBeVisible();

    expect(heroRequests).toHaveLength(0);
    const priorityImages = page.locator('img[fetchpriority="high"]');
    for (let index = 0; index < await priorityImages.count(); index += 1) {
      await expect(priorityImages.nth(index)).toBeVisible();
    }
    await context.close();
  });

  test('desktop landing requests only the active theme hero', async ({
    browserName,
    page,
  }) => {
    test.skip(browserName !== 'chromium', 'The desktop project profile uses Chromium.');
    const heroRequests: string[] = [];
    page.on('request', (request) => {
      const pathname = new URL(request.url()).pathname;
      if (/\/images\/landing-hero(?:-dark)?\.webp$/.test(pathname)) {
        heroRequests.push(pathname);
      }
    });

    await page.goto(appUrl);
    await expect(page.getByRole('heading', { name: /ابدأ رحلتك التعليمية/ })).toBeVisible();

    expect([...new Set(heroRequests)]).toEqual(['/images/landing-hero.webp']);
  });
});

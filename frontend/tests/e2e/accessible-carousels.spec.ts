import { expect, test, type Page } from '@playwright/test';

import { appUrl } from './e2e-contract-helpers';

async function openTeacherCarousel(page: Page) {
  await page.route('**/api/**', (route) =>
    route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Use the local carousel fallback.' }),
    }),
  );
  await page.goto(appUrl);
  const carousel = page.getByRole('region', { name: 'معلمو منصة مسار' });
  await carousel.scrollIntoViewIfNeeded();
  await expect(carousel).toBeVisible();
  return carousel;
}

async function currentTeacherLabel(page: Page) {
  return page.locator('[aria-label^="المعلم "][aria-label*=" من "]')
    .getAttribute('aria-label');
}

test('teacher carousel exposes working keyboard, previous, next, and pause controls', async ({
  page,
}) => {
  const carousel = await openTeacherCarousel(page);
  const initial = await currentTeacherLabel(page);

  await page.getByRole('button', { name: 'المعلم التالي' }).click();
  expect(await currentTeacherLabel(page)).not.toBe(initial);
  await page.getByRole('button', { name: 'المعلم السابق' }).click();
  expect(await currentTeacherLabel(page)).toBe(initial);

  await carousel.focus();
  await page.keyboard.press('ArrowLeft');
  expect(await currentTeacherLabel(page)).not.toBe(initial);

  const pause = page.getByRole('button', { name: 'إيقاف الحركة التلقائية' });
  await expect(pause).toBeVisible();
  await pause.click();
  const pausedAt = await currentTeacherLabel(page);
  await page.waitForTimeout(2_800);
  expect(await currentTeacherLabel(page)).toBe(pausedAt);
  await expect(page.getByRole('button', { name: 'تشغيل الحركة التلقائية' }))
    .toBeVisible();
});

test('reduced motion prevents automatic carousel changes', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await openTeacherCarousel(page);
  const initial = await currentTeacherLabel(page);

  await page.waitForTimeout(2_800);

  expect(await currentTeacherLabel(page)).toBe(initial);
});

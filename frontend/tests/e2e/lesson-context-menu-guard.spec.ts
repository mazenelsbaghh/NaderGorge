import { expect, test } from '@playwright/test';

import {
  apiUrl,
  appUrl,
  installAuthAndGoto,
  login,
  seedE2E,
} from './e2e-contract-helpers';

type MockLessonPackage = {
  lessonId: string;
  packageId: string;
};

function dispatchContextMenu(selector: string, button = 2, ctrlKey = false) {
  const target = document.querySelector(selector);
  if (!target) throw new Error(`Missing context-menu target: ${selector}`);

  const event = new MouseEvent('contextmenu', {
    bubbles: true,
    button,
    cancelable: true,
    ctrlKey,
  });
  target.dispatchEvent(event);
  return event.defaultPrevented;
}

function dispatchKeyboardShortcut({
  key,
  ctrlKey = false,
  shiftKey = false,
}: {
  key: string;
  ctrlKey?: boolean;
  shiftKey?: boolean;
}) {
  const event = new KeyboardEvent('keydown', {
    bubbles: true,
    cancelable: true,
    ctrlKey,
    key,
    shiftKey,
  });
  document.dispatchEvent(event);
  return event.defaultPrevented;
}

test.describe('lesson video context-menu guard', () => {
  test('rejects a copied embed URL before requesting backend material', async ({ request }) => {
    const response = await request.get(`${appUrl}/api/video/embed?s=00000000-0000-0000-0000-000000000000`);

    expect(response.status()).toBe(403);
    expect(await response.text()).toContain('Embed must be loaded within Massar Academy');
  });

  // Production regression 2026-09-01: a mouse context menu exposed a browser
  // extension's download entry outside the video player on the lesson page.
  test('blocks the mouse menu across a viewable lesson and cleans up after client navigation', async ({ page, request }) => {
    await seedE2E(request);

    const clearDevices = await request.post(`${apiUrl}/e2e/clear-devices`, {
      data: { phoneNumber: '20000000001' },
    });
    expect(clearDevices.ok()).toBeTruthy();

    const setupResponse = await request.post(`${apiUrl}/e2e/setup-mock-package`);
    expect(setupResponse.ok()).toBeTruthy();
    const mockPackage = await setupResponse.json() as MockLessonPackage;

    const grantResponse = await request.post(`${apiUrl}/e2e/grant-package`, {
      data: { packageId: mockPackage.packageId },
    });
    expect(grantResponse.ok()).toBeTruthy();

    const student = await login(request, 'student');
    await installAuthAndGoto(
      page,
      student.accessToken,
      student.user,
      `${appUrl}/student/packages/${mockPackage.packageId}/lessons/${mockPackage.lessonId}`,
    );
    await expect(page.getByRole('heading', { name: 'E2E Lesson' })).toBeVisible();

    await expect.poll(() => page.evaluate(dispatchContextMenu, 'main')).toBe(true);
    await expect.poll(() => page.evaluate(dispatchKeyboardShortcut, { key: 's', ctrlKey: true })).toBe(true);
    await expect.poll(() => page.evaluate(dispatchKeyboardShortcut, { key: 'i', ctrlKey: true, shiftKey: true })).toBe(true);

    const editableMenuWasBlocked = await page.evaluate(() => {
      const textarea = document.createElement('textarea');
      document.body.appendChild(textarea);
      const event = new MouseEvent('contextmenu', {
        bubbles: true,
        button: 2,
        cancelable: true,
      });
      textarea.dispatchEvent(event);
      textarea.remove();
      return event.defaultPrevented;
    });
    expect(editableMenuWasBlocked).toBe(true);

    const documentMarker = `lesson-guard-${Date.now()}`;
    await page.evaluate((marker) => {
      (window as Window & { __lessonGuardDocumentMarker?: string }).__lessonGuardDocumentMarker = marker;
    }, documentMarker);

    await page.getByRole('button', { name: 'إظهار القوائم' }).click();
    await page.getByRole('link', { name: 'باقاتي' }).first().click();
    await expect(page).toHaveURL(/\/student\/packages\/?$/);
    expect(await page.evaluate(
      () => (window as Window & { __lessonGuardDocumentMarker?: string }).__lessonGuardDocumentMarker,
    )).toBe(documentMarker);

    await expect.poll(() => page.evaluate(dispatchContextMenu, 'main')).toBe(false);
    await expect.poll(() => page.evaluate(dispatchKeyboardShortcut, { key: 's', ctrlKey: true })).toBe(false);
  });
});

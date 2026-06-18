import { expect, test } from '@playwright/test';

test.describe('video session counting', () => {
  test('older player stops and offers reload when a newer session supersedes it', async ({ page, request }) => {
    test.setTimeout(60_000);

    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000001' },
    });
    const setupResponse = await request.post('http://localhost:5245/api/e2e/setup-mock-package');
    expect(setupResponse.ok()).toBeTruthy();
    const mockPackage = await setupResponse.json();
    await request.post('http://localhost:5245/api/e2e/grant-package', {
      data: { packageId: mockPackage.packageId },
    });

    await page.goto('http://localhost:8739/login');
    await page.locator('input[type="tel"]').fill('20000000001');
    await page.locator('input[type="password"]').fill('password');
    await page.locator('button[type="submit"]').click({ force: true });
    await expect(page).toHaveURL(/\/(student|onboarding)$/);

    const progressRequests: Array<{
      sessionId: string;
      progressSequence: number;
      secondsWatched: number;
    }> = [];
    await page.route('**/api/student/video-session/*/track-progress', async (route) => {
      progressRequests.push(route.request().postDataJSON());
      if (progressRequests.length === 1) {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ success: false, errors: ['TEMPORARY_FAILURE'] }),
        });
        return;
      }

      await route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({
          success: false,
          message: 'Playback session was superseded',
          errors: ['SESSION_SUPERSEDED'],
        }),
      });
    });

    await page.goto(`http://localhost:8739/student/packages/${mockPackage.packageId}`);
    await page.getByText('E2E Term').first().click();
    await page.getByText('E2E Section').first().click();
    await page.getByText('E2E Lesson').first().click();
    await page.getByRole('button', { name: 'تحميل وتشغيل الفيديو' }).click();
    await expect(page.locator('iframe')).toHaveCount(1);

    await page.evaluate(() => {
      window.postMessage({
        source: 'video-embed',
        type: 'ready',
        data: { duration: 100, volume: 100, isMuted: false, provider: 'youtube' },
      }, window.location.origin);
      window.postMessage({
        source: 'video-embed',
        type: 'stateChange',
        data: { isPlaying: true, state: 1 },
      }, window.location.origin);
    });

    await expect(page.getByText('تم فتح الفيديو في تبويب أو جهاز أحدث. أعد تحميل الفيديو للمتابعة هنا.')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByRole('button', { name: 'إعادة تحميل الفيديو' })).toBeVisible();
    expect(progressRequests).toHaveLength(2);
    expect(progressRequests[1]).toMatchObject({
      sessionId: progressRequests[0].sessionId,
      progressSequence: progressRequests[0].progressSequence,
      secondsWatched: progressRequests[0].secondsWatched,
    });
  });
});

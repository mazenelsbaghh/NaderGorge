import { expect, test, type Route } from '@playwright/test';
import { createDevToolsSuspensionScript } from '../../src/lib/video-embed-devtools-guard';
import { embedSelector, json, openLesson } from '../fixtures/lesson-playback';

const protectedMessage = 'أغلق أدوات المطوّر، ثم اضغط «متابعة المشاهدة» للرجوع إلى نفس موضع الفيديو.';

test.describe('inspection protection (synthetic HTTP and media)', () => {
  for (const shortcut of ['F12', 'Control+Shift+i', 'Meta+Alt+i', 'Control+u']) {
    test(`${shortcut} outside the iframe suspends playback and continues in the same session`, async ({ page }) => {
      const playback = await openLesson(page, async () => {
        await page.route('**/api/video/embed?*', route => route.fulfill({
          contentType: 'text/html',
          body: `<body>Playing test video<script>
            window.commands = [];
            addEventListener('message', event => window.commands.push(event.data));
            parent.postMessage({source:'video-embed',type:'ready',data:{duration:600,provider:'youtube'}},location.origin);
            parent.postMessage({source:'video-embed',type:'stateChange',data:{isPlaying:true}},location.origin);
          </script></body>`,
        }));
      });
      const originalSessionCount = playback.sessions.length;
      const frame = page.frames().find(candidate => candidate.url().includes('/api/video/embed'))!;
      await frame.evaluate(() => parent.postMessage({
        source: 'video-embed', type: 'timeUpdate', data: { currentTime: 123, duration: 600, playbackRate: 1.25 },
      }, location.origin));
      await page.getByRole('heading', { name: playback.lesson.title, exact: true }).click();
      await page.keyboard.press(shortcut);
      await expect(page.getByText(protectedMessage, { exact: true })).toBeVisible();
      await expect(page.locator(embedSelector)).toHaveCount(0);

      await page.getByRole('button', { name: 'متابعة المشاهدة', exact: true }).click();
      await expect(page.frameLocator(embedSelector).getByText('Playing test video')).toBeVisible();
      const resumedFrame = page.frames().find(candidate => candidate.url().includes('/api/video/embed'))!;
      await expect.poll(() => resumedFrame.evaluate(() => (window as unknown as {
        commands: Array<{ type: string; time?: number; rate?: number }>;
      }).commands)).toEqual(expect.arrayContaining([
        expect.objectContaining({ type: 'seekTo', time: 123 }),
        expect.objectContaining({ type: 'setPlaybackRate', rate: 1.25 }),
        expect.objectContaining({ type: 'play' }),
      ]));
      expect(playback.sessions).toHaveLength(originalSessionCount);

      await page.getByRole('heading', { name: playback.lesson.title, exact: true }).click();
      await page.keyboard.press(shortcut);
      await expect(page.getByRole('button', { name: 'متابعة المشاهدة', exact: true })).toBeVisible();
      await expect(page.locator(embedSelector)).toHaveCount(0);
    });
  }

  test('inspection inside the embed uses the same resumable protection state', async ({ page }) => {
    await openLesson(page, async () => {
      await page.route('**/api/video/embed?*', route => route.fulfill({
        contentType: 'text/html',
        body: `<body tabindex="0">Playing test video<script>
          function suspendTestVideo() {}
          ${createDevToolsSuspensionScript('suspendTestVideo')}
          parent.postMessage({source:'video-embed',type:'ready',data:{duration:600,provider:'youtube'}},location.origin);
        </script></body>`,
      }));
    });
    await page.frameLocator(embedSelector).locator('body').press('F12');
    await expect(page.getByText(protectedMessage, { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'متابعة المشاهدة', exact: true }).click();
    await expect(page.frameLocator(embedSelector).getByText('Playing test video')).toBeVisible();
  });

  test('a late session response cannot reopen a suspended player', async ({ page }) => {
    const playback = await openLesson(page);
    let pendingSession: Route | undefined;
    await page.route('**/api/student/video-session', route => {
      if (pendingSession) return route.fallback();
      pendingSession = route;
    });
    await page.getByRole('navigation', { name: 'فيديوهات الدرس' }).getByRole('button', { name: /Part 3/ }).click();
    await expect.poll(() => pendingSession !== undefined).toBe(true);
    await page.getByRole('heading', { name: playback.lesson.title, exact: true }).click();
    await page.keyboard.press('F12');
    await json(pendingSession!, {
      sessionId: 'late-session', provider: 'bunny', durationSeconds: 600, isPreview: true,
      expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      watchInfo: { currentCount: 0, maxCount: 5, isLocked: false, totalTrackedSeconds: 0 },
    });
    await expect(page.getByText(protectedMessage, { exact: true })).toBeVisible();
    await expect(page.locator(embedSelector)).toHaveCount(0);
    await page.getByRole('button', { name: 'متابعة المشاهدة', exact: true }).click();
    await expect(page.frameLocator(embedSelector).getByText('Playing test video')).toBeVisible();
  });

  test('ordinary shortcuts, focus changes, and resizing keep the player mounted', async ({ page }) => {
    const playback = await openLesson(page);
    await page.getByRole('heading', { name: playback.lesson.title, exact: true }).click();
    await page.keyboard.press('Control+c');
    await page.setViewportSize({ width: 800, height: 600 });
    await page.evaluate(() => {
      document.body.style.zoom = '0.8';
      window.dispatchEvent(new Event('blur'));
    });
    await expect(page.frameLocator(embedSelector).getByText('Playing test video')).toBeVisible();
    expect(await playback.originalFrame.evaluate(frame => frame.isConnected)).toBe(true);
    await expect(page.getByRole('button', { name: 'متابعة المشاهدة', exact: true })).toHaveCount(0);
  });

  test('resuming saves actual watch time and never credits the inspection pause', async ({ page }) => {
    const requests: Array<{ secondsWatched: number }> = [];
    const playback = await openLesson(page, async () => {
      await page.route('**/api/student/video-session/*/track-progress', async route => {
        requests.push(route.request().postDataJSON());
        await json(route, {
          currentCount: 0, maxCount: 5, isLocked: false, viewRegistered: false,
          sessionHasRegisteredView: false, totalTrackedSeconds: 1, learningWatchedSeconds: 1,
          thresholdSeconds: 480, sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        });
      });
    }, { trackProgress: true });
    const frame = page.frames().find(candidate => candidate.url().includes('/api/video/embed'))!;
    await frame.evaluate(() => parent.postMessage({
      source: 'video-embed', type: 'timeUpdate', data: { currentTime: 100, duration: 600, playbackRate: 1 },
    }, location.origin));
    await page.waitForTimeout(1_250);
    await frame.evaluate(() => parent.postMessage({
      source: 'video-embed', type: 'timeUpdate', data: { currentTime: 101.25, duration: 600, playbackRate: 1 },
    }, location.origin));
    await page.getByRole('heading', { name: playback.lesson.title, exact: true }).click();
    await page.keyboard.press('F12');
    await expect.poll(() => requests.length).toBeGreaterThan(0);
    const watchedSeconds = requests.reduce((total, request) => total + request.secondsWatched, 0);
    expect(watchedSeconds).toBeGreaterThan(0);
    expect(watchedSeconds).toBeLessThan(3);
    await page.waitForTimeout(1_250);
    await page.getByRole('button', { name: 'متابعة المشاهدة', exact: true }).click();
    await expect(page.frameLocator(embedSelector).getByText('Playing test video')).toBeVisible();
    expect(requests.reduce((total, request) => total + request.secondsWatched, 0)).toBe(watchedSeconds);
  });
});

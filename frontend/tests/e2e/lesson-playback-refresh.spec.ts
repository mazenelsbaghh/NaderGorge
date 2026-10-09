import { expect, test, type Route } from '@playwright/test';
import { embedSelector, json, lessonApi, openLesson } from '../fixtures/lesson-playback';

test.describe('lesson playback continuity (synthetic HTTP and SignalR)', () => {
  for (const code of [410, 409]) {
    test(`2026-10-09 paused cleanup before HLS authorization ${code} preserves the correct recovery decision`, async ({ page }) => {
      const playback = await openLesson(page, async () => {
        await page.route('**/api/video/embed?*', route => route.fulfill({
          contentType: 'text/html',
          body: `<body>Playing test video<script>
            window.playerCommands=[];
            window.addEventListener('message',event=>window.playerCommands.push(event.data));
            parent.postMessage({source:'video-embed',type:'ready',data:{duration:600,provider:'bunny-hls'}},location.origin);
            parent.postMessage({source:'video-embed',type:'stateChange',data:{isPlaying:true}},location.origin);
          </script></body>`,
        }));
      });
      const frame = page.frameLocator(embedSelector);
      await frame.locator('body').evaluate((_, status) => {
        const post = (type: string, data: object) => parent.postMessage({ source: 'video-embed', type, data }, location.origin);
        post('timeUpdate', { currentTime: 87, duration: 600, playbackRate: 1.5 });
        post('stateChange', { isPlaying: false, state: 2 });
        post('error', { provider: 'bunny-hls', code: status, phase: 'source_authorization', wasPlaying: true, message: 'Session replaced' });
      }, code);
      if (code === 409) {
        await expect(page.getByText('Session replaced', { exact: true })).toBeVisible();
        expect(playback.sessions.length).toBe(playback.originalSessionCount);
        return;
      }
      await expect.poll(() => playback.sessions.length).toBe(playback.originalSessionCount + 1);
      await expect.poll(() => frame.locator('body').evaluate(() =>
        (window as unknown as { playerCommands: Array<{ type: string; time?: number; rate?: number }> }).playerCommands
          .filter(command => ['seekTo', 'setPlaybackRate', 'play'].includes(command.type))
      )).toEqual([{ type: 'seekTo', time: 87 }, { type: 'setPlaybackRate', rate: 1.5 }, { type: 'play' }]);
    });
  }

  test('comment events refresh comments without refetching the lesson or restarting playback', async ({ page }) => {
    const playback = await openLesson(page);
    let detailReads = 0;
    let commentReads = 0;
    await page.route(lessonApi, route => { detailReads++; return json(route, playback.lesson); });
    await page.route('**/api/content/lessons/*/comments?*', route => { commentReads++; return json(route, []); });
    playback.notify('LessonCommentApproved');
    await expect.poll(() => commentReads).toBeGreaterThan(0);
    expect(detailReads).toBe(0);
    expect(await playback.originalFrame.evaluate(frame => frame.isConnected)).toBe(true);
    expect(playback.sessions.length).toBe(playback.originalSessionCount);
  });

  // Production regression 2026-09-06: background invalidation showed the full
  // lesson skeleton, destroyed the playing iframe and reset to the first part.
  test('pending refresh and reordered metadata retain the same playing iframe and session', async ({ page }) => {
    const playback = await openLesson(page);
    let pending: Route | undefined;
    await page.route(lessonApi, route => { pending = route; });
    playback.notify();
    await expect.poll(() => pending !== undefined).toBe(true);
    expect(await playback.originalFrame.evaluate(frame => frame.isConnected)).toBe(true);
    const updated = {
      ...playback.lesson, title: 'Updated lesson',
      videos: [playback.lesson.videos[1], playback.lesson.videos[2], playback.lesson.videos[0]],
    };
    await json(pending!, updated);
    await expect(page.getByRole('heading', { name: 'Updated lesson', exact: true })).toBeVisible();
    await expect(page.locator('[aria-current="step"]')).toContainText('Part 2');
    expect(await playback.originalFrame.evaluate(frame => frame.isConnected)).toBe(true);
    expect(playback.sessions.length).toBe(playback.originalSessionCount);
  });

  for (const status of [503, 429, 'network'] as const) {
    test(`temporary ${status} refresh failure retains playback and retry applies updated data`, async ({ page }) => {
      const playback = await openLesson(page);
      await page.route(lessonApi, route => status === 'network' ? route.abort('failed') : json(route, null, status));
      playback.notify();
      await expect(page.getByRole('status').filter({ hasText: 'تعذر تحديث بيانات الدرس' })).toBeVisible();
      expect(await playback.originalFrame.evaluate(frame => frame.isConnected)).toBe(true);
      await page.route(lessonApi, route => json(route, { ...playback.lesson, title: 'Recovered lesson' }));
      await page.getByRole('button', { name: 'إعادة المحاولة', exact: true }).click();
      await expect(page.getByRole('heading', { name: 'Recovered lesson', exact: true })).toBeVisible();
      await expect(page.locator('[aria-current="step"]')).toContainText('Part 2');
      expect(await playback.originalFrame.evaluate(frame => frame.isConnected)).toBe(true);
      expect(playback.sessions.length).toBe(playback.originalSessionCount);
    });
  }

  for (const status of [403, 404]) {
    test(`definitive ${status} refresh response removes protected playback`, async ({ page }) => {
      const playback = await openLesson(page);
      await page.route(lessonApi, route => json(route, null, status));
      playback.notify();
      await expect(page.getByRole('heading', { name: 'الدرس غير متاح', exact: true })).toBeVisible();
      await expect(page.locator(embedSelector)).toHaveCount(0);
      expect(await playback.originalFrame.evaluate(frame => frame.isConnected)).toBe(false);
    });
  }

  test('removing the selected video falls back safely even when its old index is out of bounds', async ({ page }) => {
    const playback = await openLesson(page);
    playback.updateLesson({ ...playback.lesson, videos: [playback.lesson.videos[0]] });
    playback.notify();
    await expect.poll(() => playback.sessions.at(-1)).toBe(playback.lesson.videos[0].id);
    await expect(page.getByRole('heading', { name: 'Part 1', exact: true })).toBeVisible();
    expect(await playback.originalFrame.evaluate(frame => frame.isConnected)).toBe(false);
  });

  test('late successful refresh cannot restore playback after access was revoked by a newer response', async ({ page }) => {
    const playback = await openLesson(page);
    let staleRequest: Route | undefined;
    await page.route(lessonApi, route => { staleRequest = route; });
    playback.notify();
    await expect.poll(() => staleRequest !== undefined).toBe(true);
    await page.route(lessonApi, route => json(route, null, 403));
    playback.notify();
    await expect(page.getByRole('heading', { name: 'الدرس غير متاح', exact: true })).toBeVisible();
    await json(staleRequest!, playback.lesson);
    await expect(page.locator(embedSelector)).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'الدرس غير متاح', exact: true })).toBeVisible();
    expect(playback.sessions.length).toBe(playback.originalSessionCount);
  });

  test('updated access flags remove playback without waiting for an HTTP error', async ({ page }) => {
    const playback = await openLesson(page);
    playback.updateLesson({ ...playback.lesson, hasAccess: false });
    playback.notify();
    await expect(page.getByText('هذه الحصة غير متاحة للشراء المنفرد.', { exact: false })).toBeVisible();
    await expect(page.locator(embedSelector)).toHaveCount(0);
    expect(await playback.originalFrame.evaluate(frame => frame.isConnected)).toBe(false);
  });
});

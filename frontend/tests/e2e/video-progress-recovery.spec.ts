import { expect, test } from '@playwright/test';
import { embedSelector, json, openLesson } from '../fixtures/lesson-playback';

test('finished playback waits for a throttled progress acknowledgement and replays the same segment', async ({ page }) => {
  const requests: Record<string, unknown>[] = [];
  let firstRequestAt = 0;
  let retriedAt = 0;
  const playback = await openLesson(page, async () => {
    await page.route('**/api/student/video-session/*/track-progress', async route => {
      const body = route.request().postDataJSON();
      requests.push(body);
      if (requests.length === 1) {
        firstRequestAt = Date.now();
        await route.fulfill({ status: 429, headers: { 'Retry-After': '3', 'Access-Control-Expose-Headers': 'Retry-After' }, json: { success: false } });
        return;
      }
      retriedAt = Date.now();
      await json(route, {
        currentCount: 0, maxCount: 5, isLocked: false, viewRegistered: false,
        totalTrackedSeconds: body.secondsWatched, learningWatchedSeconds: body.secondsWatched,
        thresholdSeconds: 480, sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(), duplicate: false,
      });
    });
  }, { trackProgress: true });
  const frame = page.frames().find(candidate => candidate.url().includes('/api/video/embed'))!;
  await frame.evaluate(() => {
    parent.postMessage({ source: 'video-embed', type: 'timeUpdate', data: { currentTime: 1, duration: 600, playbackRate: 1 } }, location.origin);
  });
  // Accrue real wall time: advancing the media position alone must not count.
  await page.waitForTimeout(1_250);
  await frame.evaluate(() => {
    parent.postMessage({ source: 'video-embed', type: 'timeUpdate', data: { currentTime: 2.25, duration: 600, playbackRate: 1 } }, location.origin);
    parent.postMessage({ source: 'video-embed', type: 'stateChange', data: { isPlaying: false, state: 'ended' } }, location.origin);
  });
  await expect.poll(() => requests.length).toBe(1);
  expect(await playback.originalFrame.evaluate(element => element.isConnected)).toBe(true);
  await expect(page.locator('[aria-current="step"]')).toContainText('Part 2');
  await expect.poll(() => playback.sessions.at(-1), { timeout: 10_000 }).toBe(playback.lesson.videos[2].id);
  expect(requests).toHaveLength(2);
  expect(requests[1]).toEqual(requests[0]);
  expect(Number(requests[0].secondsWatched)).toBeGreaterThan(0);
  expect(retriedAt - firstRequestAt).toBeGreaterThanOrEqual(2_900);
  await expect(page.locator(embedSelector)).toHaveCount(1);
});

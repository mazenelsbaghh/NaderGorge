import { expect, test, type Route } from '@playwright/test';
import { embedSelector, json, openLesson } from '../fixtures/lesson-playback';

function progressFixture() {
  return {
    '96000000-0000-0000-0000-000000000011': { durationSeconds: 600, learningWatchedSeconds: 600 },
    '96000000-0000-0000-0000-000000000012': { durationSeconds: 600, learningWatchedSeconds: 300 },
    '96000000-0000-0000-0000-000000000013': { durationSeconds: 600, learningWatchedSeconds: 0 },
  };
}

test.describe('partial watch progress (synthetic HTTP and media)', () => {
  test('half a video remains half after reopening and cannot leak into another part while its session loads', async ({ page }) => {
    const progressByVideo = progressFixture();
    const playback = await openLesson(page, undefined, { progressByVideo });
    const videoProgress = page.getByRole('progressbar', { name: 'شاهدت من الفيديو', exact: true });
    await expect(videoProgress).toHaveAttribute('aria-valuenow', '50');
    await expect(page.getByRole('progressbar', { name: 'تقدّم الحصة بالكامل', exact: true })).toHaveAttribute('aria-valuenow', '50');
    let pendingSession: Route | undefined;
    await page.route('**/api/student/video-session', route => {
      if (route.request().postDataJSON().lessonVideoId !== playback.lesson.videos[2].id) return route.fallback();
      pendingSession = route;
    });
    const navigation = page.getByRole('navigation', { name: 'فيديوهات الدرس' });
    await navigation.getByRole('button', { name: /Part 3/ }).click();
    await expect.poll(() => pendingSession !== undefined).toBe(true);
    await expect(videoProgress).toHaveAttribute('aria-valuenow', '0');
    await json(pendingSession!, {
      sessionId: 'third-part-session', provider: 'bunny', durationSeconds: 600, isPreview: true,
      expiresAt: new Date(Date.now() + 3_600_000).toISOString(), thresholdPercentage: 80,
      watchInfo: { currentCount: 0, maxCount: 5, isLocked: false, totalTrackedSeconds: 0, learningWatchedSeconds: 0 },
    });
    await navigation.getByRole('button', { name: /Part 2/ }).click();
    await expect(videoProgress).toHaveAttribute('aria-valuenow', '50');
    await page.reload();
    await navigation.getByRole('button', { name: /Part 2/ }).click();
    await expect(videoProgress).toHaveAttribute('aria-valuenow', '50');
  });

  test('acknowledged partial playback crosses 50 percent, pauses without accumulating and survives reopening', async ({ page }) => {
    const progressByVideo: Record<string, { durationSeconds: number; learningWatchedSeconds: number }> = progressFixture();
    const activeId = '96000000-0000-0000-0000-000000000012';
    progressByVideo[activeId].learningWatchedSeconds = 299.5;
    const requests: Array<{ secondsWatched: number; playbackRate: number }> = [];
    await openLesson(page, async () => {
      await page.route('**/api/student/video-session/*/track-progress', async route => {
        const request = route.request().postDataJSON();
        requests.push(request);
        const videoId = new URL(route.request().url()).pathname.split('/').at(-2)!;
        const progress = progressByVideo[videoId];
        progress.learningWatchedSeconds += request.secondsWatched * request.playbackRate;
        await json(route, { currentCount: 0, maxCount: 5, isLocked: false, viewRegistered: false,
          sessionHasRegisteredView: false, totalTrackedSeconds: progress.learningWatchedSeconds,
          learningWatchedSeconds: progress.learningWatchedSeconds, thresholdSeconds: 480,
          sessionExpiresAt: new Date(Date.now() + 3_600_000).toISOString(), duplicate: false });
      });
    }, { trackProgress: true, progressByVideo });
    const videoProgress = page.getByRole('progressbar', { name: 'شاهدت من الفيديو', exact: true });
    await expect(videoProgress).toHaveAttribute('aria-valuenow', '49');
    const frame = page.frames().find(candidate => candidate.url().includes('/api/video/embed'))!;
    await frame.evaluate(() => parent.postMessage({ source: 'video-embed', type: 'timeUpdate', data: { currentTime: 300, duration: 600, playbackRate: 1 } }, location.origin));
    await page.waitForTimeout(1_250);
    await frame.evaluate(() => {
      parent.postMessage({ source: 'video-embed', type: 'timeUpdate', data: { currentTime: 301.25, duration: 600, playbackRate: 1 } }, location.origin);
      parent.postMessage({ source: 'video-embed', type: 'stateChange', data: { isPlaying: false, state: 'paused' } }, location.origin);
    });
    await expect(videoProgress).toHaveAttribute('aria-valuenow', '50');
    expect(progressByVideo[activeId].learningWatchedSeconds).toBeLessThan(302);
    const pausedRequestCount = requests.length;
    await page.waitForTimeout(1_000);
    expect(requests).toHaveLength(pausedRequestCount);
    await page.reload();
    await page.getByRole('navigation', { name: 'فيديوهات الدرس' }).getByRole('button', { name: /Part 2/ }).click();
    await expect(videoProgress).toHaveAttribute('aria-valuenow', '50');
    await expect(page.locator(embedSelector)).toHaveCount(1);
  });
});

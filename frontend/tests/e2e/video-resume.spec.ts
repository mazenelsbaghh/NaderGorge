import { expect, test } from '@playwright/test';
import { embedSelector, openLesson } from '../fixtures/lesson-playback';

for (const provider of ['youtube', 'youtube-hls', 'bunny', 'bunny-hls']) {
  test(`${provider} waits for play at the last position after reopening a lesson`, async ({ page }) => {
    await openLesson(page, async () => {
      await page.route('**/api/video/embed?*', route => route.fulfill({
        contentType: 'text/html',
        body: `<body>Playing test video
          <output id="position"></output><output id="playback"></output>
          <script>
          const provider = ${JSON.stringify(provider)};
          const position = document.getElementById('position');
          const playback = document.getElementById('playback');
          const report = (type, data) => parent.postMessage({source:'video-embed', type, data}, location.origin);
          position.value = new URLSearchParams(location.hash.slice(1)).get('resume') || '0';
          playback.value = 'paused';
          addEventListener('message', ({ data }) => {
            if (data.type === 'seekTo') position.value = data.time;
            if (data.type === 'play') {
              playback.value = 'playing';
              report('stateChange', { state: 1, isPlaying: true });
            }
          });
          report('ready', { duration: 600, provider });
          </script></body>`,
      }));
    }, { provider, trackProgress: true });
    const player = page.getByRole('region', { name: 'مشغل الفيديو' });
    const embed = page.frameLocator(embedSelector);
    await player.press('k');
    await expect(embed.locator('#playback')).toHaveText('playing');
    const frame = page.frames().find(candidate => candidate.url().includes('/api/video/embed'))!;
    await frame.evaluate(() => parent.postMessage({
      source: 'video-embed', type: 'timeUpdate', data: { currentTime: 125, duration: 600 },
    }, location.origin));
    await page.waitForFunction(() => Object.keys(localStorage).some(key =>
      key.startsWith('massar:video-resume:') && JSON.parse(localStorage.getItem(key)!).seconds === 125));
    await frame.evaluate(() => parent.dispatchEvent(new MessageEvent('message', {
      origin: location.origin,
      source: window,
      data: { source: 'video-embed', type: 'timeUpdate', data: { currentTime: 127, duration: 600 } },
    })));

    await page.reload();
    await page.getByRole('navigation', { name: 'فيديوهات الدرس' }).getByRole('button', { name: /Part 2/ }).click();
    await expect(embed.locator('#position')).toHaveText('127');
    await expect(embed.locator('#playback')).toHaveText('paused');
    await player.press('k');
    await expect(embed.locator('#playback')).toHaveText('playing');
    await expect(embed.locator('#position')).toHaveText('127');
  });
}

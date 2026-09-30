import assert from 'node:assert/strict';
import { test } from 'node:test';
import { chromium } from '@playwright/test';

const previewUrl = process.env.HLS_PREVIEW_URL || 'http://127.0.0.1:8738/dev/youtube-hls';

function isQualityMedia(response, quality) {
  const url = new URL(response.url());
  return url.pathname === '/api/dev/youtube-hls'
    && url.searchParams.get('media') === quality
    && url.searchParams.has('part')
    && response.ok();
}

test('live YouTube HLS quality changes keep playback and speed in the platform player',
  { timeout: 60_000 }, async () => {
    const browser = await chromium.launch(process.env.HLS_CHROME_EXECUTABLE
      ? { executablePath: process.env.HLS_CHROME_EXECUTABLE, headless: true }
      : { channel: 'chrome', headless: true });
    try {
      const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      page.on('response', response => {
        if (response.url().includes('/api/dev/youtube-hls') && response.status() >= 400) {
          errors.push(`Preview media HTTP ${response.status()}`);
        }
      });

      await page.goto(previewUrl, { waitUntil: 'domcontentloaded' });
      const quality = page.getByRole('combobox', { name: 'جودة الفيديو' });
      await quality.waitFor({ timeout: 20_000 });
      const available = await quality.locator('option').evaluateAll(options =>
        options.map(option => option.value));
      assert.ok(available.includes('144') && available.includes('360'),
        'The live preview must expose both 144p and 360p');

      const video = page.frameLocator('iframe[src*="youtube-hls"]').locator('video');
      await page.getByRole('button', { name: 'تشغيل الفيديو', exact: true }).click();
      await video.evaluate(element => new Promise((resolve, reject) => {
        const timeout = setTimeout(() => reject(new Error('Live HLS playback did not advance')), 20_000);
        const check = () => {
          if (element.currentTime > 1) { clearTimeout(timeout); resolve(); }
          else setTimeout(check, 100);
        };
        check();
      }));

      await Promise.all([
        page.waitForResponse(response => isQualityMedia(response, '144'), { timeout: 15_000 }),
        quality.selectOption('144'),
      ]);
      await page.getByRole('combobox', { name: 'سرعة التشغيل' }).selectOption('1.25');
      const before = await video.evaluate(element => ({
        time: element.currentTime, source: element.currentSrc,
      }));
      await Promise.all([
        page.waitForResponse(response => isQualityMedia(response, '360'), { timeout: 15_000 }),
        quality.selectOption('360'),
      ]);
      await video.evaluate((element, previousTime) => new Promise((resolve, reject) => {
        const timeout = setTimeout(() => reject(new Error('Playback stalled after quality change')), 15_000);
        const check = () => {
          if (element.currentTime > previousTime + 1) { clearTimeout(timeout); resolve(); }
          else setTimeout(check, 100);
        };
        check();
      }), before.time);
      const after = await video.evaluate(element => ({
        time: element.currentTime, source: element.currentSrc,
        paused: element.paused, rate: element.playbackRate, error: element.error?.message ?? null,
      }));
      assert.equal(await quality.inputValue(), '360');
      assert.equal(after.source, before.source);
      assert.ok(after.time > before.time + 1);
      assert.equal(after.paused, false);
      assert.equal(after.rate, 1.25);
      assert.equal(after.error, null);

      await page.getByRole('button', { name: 'معاينة عرض الموبايل' }).click();
      assert.ok((await page.getByRole('region', { name: 'معاينة مشغل الدرس' }).boundingBox()).width <= 390);
      assert.ok(await quality.isVisible());
      assert.deepEqual(errors, []);
    } finally {
      await browser.close();
    }
  });

import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium } from '@playwright/test';
import { youtubeQualityPreviewScript, youtubeQualityPreviewStyles } from '../../src/lib/youtube-quality-preview.ts';

// Regression: opening quality punched a hole through the black cover, exposing
// YouTube's controls and allowing taps through it (September 2026 screenshots).
for (const device of [
  { name: 'phone', width: 390, height: 360, hasTouch: true },
  { name: 'desktop', width: 960, height: 540, hasTouch: false },
  { name: 'compact landscape', width: 600, height: 220, hasTouch: true },
]) {
  test(`quality keeps native controls covered on ${device.name}`, async () => {
    const browser = await chromium.launch(process.env.QUALITY_CHROME_EXECUTABLE
      ? { executablePath: process.env.QUALITY_CHROME_EXECUTABLE } : {});
    try {
      const page = await browser.newPage({ viewport: device, hasTouch: device.hasTouch });
      await page.route('https://quality.test/', route => route.fulfill({
        contentType: 'text/html',
        body: `<style>
          * { box-sizing:border-box; margin:0; }
          html, body { width:100%; height:100%; overflow:hidden; }
          #native-controls { position:absolute; inset:0; background:red; }
          #click-overlay { position:absolute; inset:0; z-index:10; }
          ${youtubeQualityPreviewStyles({ bottomCoverPercent: 20, mobileBottomCoverPercent: 10 })}
        </style>
        <body class="quality-started"><button id="native-controls">Native controls</button>
        <div id="click-overlay"></div><script>
          var nativeClicks = 0;
          document.querySelector('#native-controls').onclick = () => nativeClicks++;
          var YT = { PlayerState: { PAUSED: 2 } };
          var player = { getPlayerState: () => 1 };
          function postToParent() {}
          ${youtubeQualityPreviewScript()}
        </script></body>`,
      }));
      await page.goto('https://quality.test/');
      const cover = page.locator('#quality-bottom-mask');
      const originalBounds = await cover.boundingBox();
      assert.ok(originalBounds);
      for (const action of ['openNativeQualityMenu', 'closeNativeQualityMenu', 'openNativeQualityMenu']) {
        await page.evaluate(type => window.postMessage({ type }, location.origin), action);
        await page.waitForFunction(open => document.body.classList.contains('quality-open') === open,
          action === 'openNativeQualityMenu');
        assert.deepEqual(await cover.boundingBox(), originalBounds);
        assert.equal(await cover.evaluate(element => getComputedStyle(element).backgroundColor), 'rgb(0, 0, 0)');
        for (const x of [4, device.width / 2, device.width - 4]) {
          const y = originalBounds.y + 8;
          assert.equal(await page.evaluate(({ x, y }) => document.elementFromPoint(x, y)?.id, { x, y }), 'quality-bottom-mask');
          if (device.hasTouch) await page.touchscreen.tap(x, y);
          else await page.mouse.click(x, y);
        }
        assert.equal(await page.evaluate(() => nativeClicks), 0);
        if (action === 'openNativeQualityMenu') {
          // The fix must preserve the uncovered menu area above the cover.
          assert.equal(await page.evaluate(x => document.elementFromPoint(x, 60)?.id, device.width / 2), 'native-controls');
        }
      }
    } finally {
      await browser.close();
    }
  });
}

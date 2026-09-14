import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { webkit } from '@playwright/test';

const baseUrl = process.env.STUDENT_HOME_TEST_URL || 'http://app.lvh.me:8738';
const user = { id: 'player-student', fullName: 'أحمد محمد', roles: ['Student'], permissions: [], profileComplete: true, allowedDomains: ['student'], allowedNavbarItems: [], authorizationVersion: 1 };
const mapSvg = '<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="800"><rect width="1200" height="800" fill="#e6f3f3"/><text x="600" y="400" text-anchor="middle" font-size="48" fill="#0a1d3d">Lesson map</text></svg>';

async function installLesson(page, fullscreenApi) {
  let sessions = 0;
  await page.addInitScript(({ user, fullscreenApi }) => {
    localStorage.setItem('accessToken', 'test-token'); localStorage.setItem('user', JSON.stringify(user));
    localStorage.setItem(`onboarding_ack_${user.id}`, '1');
    if (fullscreenApi === 'missing' || fullscreenApi === 'legacy') {
      Object.defineProperty(HTMLElement.prototype, 'requestFullscreen', { value: undefined, configurable: true });
      Object.defineProperty(HTMLElement.prototype, 'webkitRequestFullscreen', { value: undefined, configurable: true });
      if (fullscreenApi === 'legacy') Object.defineProperty(HTMLElement.prototype, 'showPopover', { value: undefined, configurable: true });
    } else if (fullscreenApi === 'stuck') {
      Object.defineProperty(HTMLElement.prototype, 'requestFullscreen', { value: () => new Promise(() => {}), configurable: true });
    }
  }, { user, fullscreenApi });
  await page.route('**/test-mindmap.svg', route => route.fulfill({ contentType: 'image/svg+xml', body: mapSvg }));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
    if (path === '/video/embed') return route.fulfill({ contentType: 'text/html', body: `<html><body style="margin:0;background:#152c45;color:white"><div style="padding:35px">Student watermark</div><script>
      window.commands=[];const send=(type,data)=>parent.postMessage({source:'video-embed',type,data},location.origin);
      addEventListener('message',e=>{commands.push(e.data);if(e.data.type==='seekTo')send('timeUpdate',{currentTime:e.data.time,duration:120});});
      setTimeout(()=>{send('ready',{provider:'bunny-hls',duration:120,volume:100});send('stateChange',{isPlaying:true});send('timeUpdate',{currentTime:15,duration:120});},100);
      addEventListener('pointerdown',()=>send('playerInteraction',{}));
      </script></body></html>` });
    let data = [];
    if (path === '/auth/session') data = { user, authorizationVersion: 1 };
    if (path === '/student/shell-bootstrap') data = { unreadNotificationsCount: 0, currentBalance: 0, gamification: { totalPoints: 0, currentStreakCount: 0, longestStreakCount: 0 }, themePreferences: {}, hasSeenTrackingCodePopup: true };
    if (path === '/content/lessons/mobile-lesson') data = {
      id: 'mobile-lesson', title: 'الحصة الأولى', summary: '', packageId: 'mobile-package', isLocked: false,
      isVideoOnlyAccess: true, resources: [], videos: [{ id: 'mobile-video', title: 'الجزء الثاني: الشرح', provider: 'bunny-hls', providerVideoId: 'test', durationSeconds: 120, hasAccess: true, maxWatchCount: 5, order: 1,
        chapters: [{ id: 'chapter', title: 'الفصل الأول', startTime: 0, endTime: 120, order: 1, summaryText: 'معلومات الفصل طويلة بما يكفي للقراءة على الموبايل. '.repeat(12), mindmapImageUrl: '/test-mindmap.svg' }] }],
    };
    if (path === '/student/video-session') { sessions++; data = { sessionId: 'test-session', expiresAt: '2099-01-01T00:00:00Z', provider: 'bunny-hls', isPreview: true, durationSeconds: 120, thresholdPercentage: 100, watchInfo: { currentCount: 0, maxCount: 5, isLocked: false, totalTrackedSeconds: 0 } }; }
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data }) });
  });
  return () => sessions;
}

for (const [width, fullscreenApi] of [[320, 'missing'], [390, 'stuck'], [375, 'legacy'], [768, 'native']]) {
  test(`mobile player ${width}px: readable aids and ${fullscreenApi} fullscreen preserve playback`, { timeout: 120000 }, async () => {
    const browser = await webkit.launch();
    try {
      const page = await browser.newPage({ viewport: { width, height: 844 }, hasTouch: true });
      page.setDefaultTimeout(15000);
      const errors = []; page.on('pageerror', error => errors.push(error.message));
      const sessionCount = await installLesson(page, fullscreenApi);
      await page.goto(`${baseUrl}/student/packages/mobile-package/lessons/mobile-lesson`);
      const root = page.locator('.secure-video-root');
      await page.getByRole('button', { name: 'معلومات الفصل', exact: true }).waitFor().catch(async error => {
        console.log('Player diagnostics:', await page.locator('body').innerText(), errors);
        throw error;
      });
      const iframe = page.frameLocator('.secure-video-root iframe');
      await iframe.locator('body').tap();
      const controls = page.locator('.secure-player-controls');
      await controls.waitFor();
      const box = await controls.boundingBox();
      assert.ok(box.height <= (width < 640 ? 104 : 120), `controls too tall: ${box.height}`);
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
      await mkdir('../artifacts/player-mobile', { recursive: true });
      await page.screenshot({ path: `../artifacts/player-mobile/controls-${width}.png`, fullPage: true });
      await page.getByRole('button', { name: 'معلومات الفصل', exact: true }).tap();
      const dialog = page.getByRole('dialog');
      await dialog.waitFor();
      assert.ok((await dialog.boundingBox()).width <= width);
      await page.getByRole('button', { name: 'إغلاق', exact: true }).tap();
      await root.getByRole('button', { name: 'الخريطة الذهنية', exact: true }).tap();
      const map = page.getByRole('dialog').locator('img');
      await map.waitFor();
      await page.waitForFunction(() => document.querySelector('dialog img')?.naturalWidth > 0);
      assert.ok((await map.boundingBox()).height > 150);
      await page.getByRole('button', { name: 'تكبير الخريطة' }).tap();
      assert.ok((await map.boundingBox()).width > width);
      await page.getByRole('button', { name: 'إغلاق', exact: true }).tap();
      await root.evaluate(el => el.scrollIntoView({ block: 'start' }));
      await iframe.locator('body').tap();
      await page.getByRole('button', { name: 'تبديل وضع ملء الشاشة' }).tap();
      await page.waitForFunction(() => Boolean(document.fullscreenElement) || document.querySelector('.secure-video-pseudo-fullscreen'));
      if (fullscreenApi !== 'native' && fullscreenApi !== 'legacy') assert.equal(await root.evaluate(el => el.matches(':popover-open')), true);
      const full = await root.boundingBox();
      assert.ok(full.width >= width - 2 && full.height >= 842, JSON.stringify(full));
      assert.ok(Math.abs(full.x) <= 2 && Math.abs(full.y) <= 2, `fullscreen is off screen: ${JSON.stringify(full)}`);
      await page.screenshot({ path: `../artifacts/player-mobile/fullscreen-${width}.png` });
      const mediaBox = await root.locator('iframe').boundingBox();
      assert.ok(mediaBox.x >= -2 && mediaBox.y >= -2, `video is off screen: ${JSON.stringify(mediaBox)}`);
      await page.touchscreen.tap(width / 2, 422);
      const slider = page.getByRole('slider', { name: 'تقدم الفيديو' });
      await slider.tap();
      if (fullscreenApi === 'legacy') {
        await page.setViewportSize({ width: 844, height: width });
        const rotated = await root.boundingBox();
        assert.ok(rotated.width >= 842 && rotated.height >= width - 2, JSON.stringify(rotated));
        assert.ok(Math.abs(rotated.x) <= 2 && Math.abs(rotated.y) <= 2, JSON.stringify(rotated));
      }
      await iframe.locator('body').tap();
      await page.getByRole('button', { name: 'تبديل وضع ملء الشاشة' }).tap();
      await page.waitForFunction(() => !document.fullscreenElement && !document.querySelector('.secure-video-pseudo-fullscreen'));
      assert.equal(sessionCount(), 1, 'fullscreen must not request another video session');
      assert.equal(await iframe.locator('body').textContent().then(s => s.includes('Student watermark')), true);
      assert.deepEqual(errors, []);
    } finally { await browser.close(); }
  });
}

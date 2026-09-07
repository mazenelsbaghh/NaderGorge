import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';
import ts from 'typescript';
import { webkit } from '@playwright/test';

for (const provider of ['hls', 'youtube']) {
test(`${provider} iframe sends real mouse and touch interactions to its parent, including fullscreen`, { timeout: 30000 }, async () => {
  const source = await readFile(new URL('../../src/app/api/video/embed/route.ts', import.meta.url), 'utf8');
  const generator = source.slice(source.indexOf('function generateBunnyHlsEmbedHtml'), source.indexOf('function configuredLegacyBunnyLibraryId'));
  const escape = source.slice(source.indexOf('function escapeHtml('), source.indexOf('function generateYouTubeEmbedHtml'));
  const youtube = source.slice(source.indexOf('function generateYouTubeEmbedHtml'), source.indexOf('function generateVkEmbedHtml'));
  const guard = await readFile(new URL('../../src/lib/video-embed-devtools-guard.ts', import.meta.url), 'utf8');
  const compiled = ts.transpileModule(generator + escape + guard + youtube, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS } }).outputText;
  const call = provider === 'hls' ? 'generateBunnyHlsEmbedHtml("https://vz-example.b-cdn.net/video/playlist.m3u8","Test","")' : 'generateYouTubeEmbedHtml("test-video", "Test", "")';
  const embed = vm.runInNewContext(compiled + '\n' + call, { URL, exports: {} });
  const hls = await readFile(new URL('../../public/vendor/hlsjs/hls.min.js', import.meta.url));
  const browser = await webkit.launch();
  try {
    for (const hasTouch of [false, true]) {
      const context = await browser.newContext({ hasTouch, viewport: { width: hasTouch ? 768 : 1200, height: 800 } });
      const page = await context.newPage();
      page.setDefaultTimeout(5000);
      await page.route('**/*', async route => {
        const url = new URL(route.request().url());
        if (url.pathname === '/embed') return route.fulfill({ contentType: 'text/html', body: embed });
        if (url.pathname.endsWith('hls.min.js')) return route.fulfill({ contentType: 'application/javascript', body: hls });
        if (url.hostname === 'vz-example.b-cdn.net') return route.fulfill({ status: 503, body: '' });
        if (url.hostname !== 'hls.test') return route.fulfill({ status: 503, body: '' });
        return route.fulfill({ contentType: 'text/html', body: `<button id="full">Fullscreen</button>
          <div id="surface" style="width:100%;height:500px"><iframe src="/embed" style="width:100%;height:100%;border:0"></iframe></div>
          <script>window.interactions=0;window.addEventListener('message',event=>{
            if(event.origin===location.origin&&event.source===document.querySelector('iframe').contentWindow&&event.data.type==='playerInteraction')window.interactions++;
          });document.querySelector('#full').onclick=()=>document.querySelector('#surface').requestFullscreen();</script>` });
      });
      await page.goto('http://hls.test/');
      const frame = page.frameLocator('iframe').locator(provider === 'hls' ? '#video' : '#click-overlay');
      await frame.waitFor();
      if (hasTouch) await frame.tap();
      else await frame.hover();
      await page.waitForFunction(() => window.interactions > 0);
      const before = await page.evaluate(() => window.interactions);
      await page.locator('#full').click();
      await page.waitForFunction(() => Boolean(document.fullscreenElement));
      if (hasTouch) await frame.tap({ position: { x: 60, y: 60 } });
      else await frame.hover({ position: { x: 60, y: 60 } });
      await page.waitForFunction(previous => window.interactions > previous, before);
      assert.ok(await page.evaluate(() => window.interactions > 0));
      await context.close();
    }
  } finally {
    await browser.close();
  }
});
}

import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';
import ts from 'typescript';
import { webkit } from '@playwright/test';

async function generateEmbed(provider) {
  const source = await readFile(new URL('../../src/app/api/video/embed/route.ts', import.meta.url), 'utf8');
  const generator = source.slice(source.indexOf('function generateBunnyHlsEmbedHtml'), source.indexOf('function configuredLegacyBunnyLibraryId'));
  const escape = source.slice(source.indexOf('function escapeHtml('), source.indexOf('function generateYouTubeEmbedHtml'));
  const youtube = source.slice(source.indexOf('function generateYouTubeEmbedHtml'), source.indexOf('function generateVkEmbedHtml'));
  const guard = await readFile(new URL('../../src/lib/video-embed-devtools-guard.ts', import.meta.url), 'utf8');
  const compiled = ts.transpileModule(generator + escape + guard + youtube, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS } }).outputText;
  const call = provider === 'hls' ? 'generateBunnyHlsEmbedHtml("https://vz-example.b-cdn.net/video/playlist.m3u8","Test","")' : 'generateYouTubeEmbedHtml("test-video", "Test", "")';
  const embed = vm.runInNewContext(compiled + '\n' + call, { URL, exports: {} });
  return embed;
}

for (const provider of ['hls', 'youtube']) {
test(`${provider} iframe sends real mouse and touch interactions to its parent, including fullscreen`, { timeout: 30000 }, async () => {
  const embed = await generateEmbed(provider);
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

// YouTube is the network boundary: only a trusted tap inside its cross-origin
// frame starts playback; scripted play reports the documented autoplay block.
const touchOnlyYouTubeSdk = `
window.YT = { PlayerState: { PLAYING: 1 }, Player: function(id, options) {
  const placeholder = document.getElementById(id);
  const iframe = document.createElement('iframe');
  iframe.style.cssText = placeholder.style.cssText;
  iframe.src = 'https://youtube.test/player';
  placeholder.replaceWith(iframe);
  let state = -1;
  this.getIframe = () => iframe;
  this.getDuration = () => 120;
  this.getCurrentTime = () => state === 1 ? 1 : 0;
  this.getVolume = () => 100;
  this.isMuted = () => false;
  this.getPlayerState = () => state;
  this.getAvailableQualityLevels = () => [];
  this.getPlaybackQuality = () => 'auto';
  this.playVideo = () => options.events.onAutoplayBlocked();
  this.pauseVideo = () => { state = 2; options.events.onStateChange({ data: state }); };
  window.addEventListener('message', event => {
    if (event.source !== iframe.contentWindow || event.origin !== 'https://youtube.test' || event.data !== 'native-play') return;
    state = 1; options.events.onStateChange({ data: state });
  });
  iframe.onload = () => options.events.onReady({ target: this });
} };
onYouTubeIframeAPIReady();`;

for (const device of [
  { name: 'iPad desktop UA', hasTouch: true, userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15) AppleWebKit/605.1.15 Version/18.0 Safari/605.1.15' },
  { name: 'Android tablet', hasTouch: true, userAgent: 'Mozilla/5.0 (Linux; Android 13; Tablet) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36' },
  { name: 'desktop autoplay blocked', hasTouch: false, userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15) AppleWebKit/605.1.15 Version/18.0 Safari/605.1.15' },
]) {
test(`YouTube direct input starts playback and resumes in fullscreen: ${device.name}`, { timeout: 30000 }, async () => {
  const embed = await generateEmbed('youtube');
  const browser = await webkit.launch();
  try {
    const context = await browser.newContext({ hasTouch: device.hasTouch, userAgent: device.userAgent, viewport: { width: 768, height: 900 } });
    const page = await context.newPage();
    page.setDefaultTimeout(5000);
    await page.route('**/*', route => {
      const url = new URL(route.request().url());
      if (url.pathname === '/embed') return route.fulfill({ contentType: 'text/html', body: embed });
      if (url.pathname === '/iframe_api') return route.fulfill({ contentType: 'application/javascript', body: touchOnlyYouTubeSdk });
      if (url.hostname === 'youtube.test') return route.fulfill({ contentType: 'text/html', body: `<button style="position:fixed;inset:0" onclick="if(event.isTrusted)parent.postMessage('native-play','https://platform.test')">YouTube play</button>` });
      if (url.hostname !== 'platform.test') return route.fulfill({ status: 503, body: '' });
      return route.fulfill({ contentType: 'text/html', body: `<style>body{margin:0}#surface{width:100%;height:500px}#surface:fullscreen{height:100vh}</style>
        <div id="surface"><iframe src="/embed" style="width:100%;height:100%;border:0"></iframe></div>
        <button id="full" onclick="document.getElementById('surface').requestFullscreen()">Fullscreen</button><script>
        window.messages=[];window.addEventListener('message',event=>{
          if(event.origin===location.origin&&event.source===document.querySelector('iframe').contentWindow)window.messages.push(event.data);
        });</script>` });
    });
    await page.goto('https://platform.test/');
    await page.waitForFunction(() => window.messages.some(message => message.type === 'ready'));
    // Tap coordinates rather than bypassing hit-testing with a dispatched event.
    const activateCenter = async () => {
      const box = await page.locator('iframe').boundingBox();
      const x = box.x + box.width / 2;
      const y = box.y + box.height / 2;
      if (device.hasTouch) await page.touchscreen.tap(x, y);
      else await page.mouse.click(x, y);
    };
    await activateCenter();
    await page.waitForFunction(() => window.messages.some(message => message.type === 'stateChange' && message.data.isPlaying), null, { timeout: 2500 });
    const embedFrame = page.frames().find(frame => frame.url().endsWith('/embed'));
    assert.equal(await embedFrame.locator('#click-overlay').isVisible(), true, 'custom interaction surface returns after playback starts');
    await page.locator('#full').click();
    await page.waitForFunction(() => Boolean(document.fullscreenElement));
    await page.evaluate(() => {
      window.messages = [];
      const embedWindow = document.querySelector('iframe').contentWindow;
      embedWindow.postMessage({ type: 'pause' }, location.origin);
      embedWindow.postMessage({ type: 'play' }, location.origin);
    });
    await page.waitForFunction(() => window.messages.some(message => message.type === 'autoplayBlocked'));
    await activateCenter();
    await page.waitForFunction(() => window.messages.some(message => message.type === 'stateChange' && message.data.isPlaying));
    await context.close();
  } finally {
    await browser.close();
  }
});
}

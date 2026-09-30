import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium } from '@playwright/test';
import { compatibleStylesheet, stylesheetOrder } from '../../scripts/browser-compatible-css.mjs';
import { browserCompatibleCssScript } from '../../src/lib/browser-compatible-css.ts';

const buildId = '00000000-0000-4000-8000-000000000001';
const stylesheets = [
  { href: '/_next/static/chunks/base.css', css: `
    @layer theme, base, utilities;
    @layer theme { :root { --surface: oklch(1 0 0); } .dark { --surface: oklch(0 0 0); } }
    @layer base { * { box-sizing: border-box; } body { margin: 0; } a { color: inherit; text-decoration: none; } }
    @layer utilities {
      .flex { display: flex; gap: 16px; } .hidden { display: none; }
      .panel { background: var(--surface); padding: 16px; border: 1px solid black; }
      .desktop { display: none; @media (width >= 768px) { display: flex; } }
      .mobile { display: flex; @media (width >= 768px) { display: none; } }
      .label { color: red !important; }
    }
    .panel { border-radius: 12px; }
    .font { background-image: url(../media/logo.svg?size=2#mark); }
  ` },
  { href: '/_next/static/chunks/login.css', css: `
    .panel { padding: 24px; }
    .label { color: blue !important; }
  ` },
];

test('route chunks preserve shared ordering and reject conflicting cascade order', () => {
  assert.deepEqual(stylesheetOrder([['font', 'base'], ['base', 'auth'], ['auth', 'login']]), ['font', 'base', 'auth', 'login']);
  assert.throws(() => stylesheetOrder([['base', 'auth'], ['auth', 'base']]), /Conflicting stylesheet order/);
});

test('in-app browser keeps navigation, RTL layout, themes and cross-chunk priority', async () => {
  const css = await compatibleStylesheet(stylesheets);
  assert.match(css, /\/_next\/static\/media\/logo\.svg\?size=2#mark/);
  const browser = await chromium.launch(process.env.BROWSER_COMPAT_CHROME_EXECUTABLE
    ? { executablePath: process.env.BROWSER_COMPAT_CHROME_EXECUTABLE }
    : { channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const compatibilityRequests = [];
    await page.route('https://compat.test/**', route => {
      if (route.request().url().endsWith(`${buildId}.css`)) {
        compatibilityRequests.push(route.request().url());
        return route.fulfill({ contentType: 'text/css', body: css });
      }
      return route.fulfill({ contentType: 'text/html', body: `<!doctype html><html dir="rtl"><head>
        <script>${browserCompatibleCssScript(buildId)}</script>
        ${stylesheets.map(sheet => `<style>${sheet.css}</style>`).join('')}
        </head><body><nav class="panel flex"><a href="#content">الرئيسية</a>
        <div class="desktop">القائمة الكبيرة</div><button class="mobile">القائمة</button>
        <span class="hidden">قائمة مغلقة</span></nav><main id="content" class="panel label">مسار</main></body></html>` });
    });
    await page.goto('https://compat.test/');
    const modern = await page.evaluate(() => !!window.CSSLayerBlockRule && CSS.supports('color', 'oklch(0.5 0.1 30)') && CSS.supports('color', 'color-mix(in srgb, red, blue)'));
    assert.equal(compatibilityRequests.length, modern ? 0 : 1, 'modern browsers must not download the compatibility bundle');
    const layout = await page.locator('nav').evaluate(nav => ({
      display: getComputedStyle(nav).display,
      padding: getComputedStyle(nav).paddingTop,
      direction: getComputedStyle(nav).direction,
      background: getComputedStyle(nav).backgroundColor,
      decoration: getComputedStyle(nav.querySelector('a')).textDecorationLine,
      radius: getComputedStyle(nav).borderTopLeftRadius,
    }));
    assert.equal(layout.display, 'flex');
    assert.equal(layout.padding, '24px');
    assert.equal(layout.direction, 'rtl');
    assert.equal(layout.decoration, 'none');
    assert.equal(layout.radius, '12px');
    assert.notEqual(layout.background, 'rgba(0, 0, 0, 0)');
    assert.equal(await page.locator('main').evaluate(el => getComputedStyle(el).color), 'rgb(255, 0, 0)');
    assert.equal(await page.locator('.hidden').isVisible(), false);
    assert.equal(await page.locator('.desktop').isVisible(), false);
    assert.equal(await page.locator('.mobile').isVisible(), true);
    await page.setViewportSize({ width: 1024, height: 768 });
    assert.equal(await page.locator('.desktop').isVisible(), true);
    assert.equal(await page.locator('.mobile').isVisible(), false);
    await page.evaluate(() => document.documentElement.classList.add('dark'));
    const darkBackground = await page.locator('nav').evaluate(el => getComputedStyle(el).backgroundColor);
    assert.notEqual(darkBackground, layout.background);
    assert.notEqual(darkBackground, 'rgba(0, 0, 0, 0)');
    console.log(`Verified ${await browser.version()}; compatibility requested: ${compatibilityRequests.length}`);
  } finally {
    await browser.close();
  }
});

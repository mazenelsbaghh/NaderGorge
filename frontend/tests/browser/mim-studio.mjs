import assert from 'node:assert/strict';
import { readFileSync, mkdirSync } from 'node:fs';
import test from 'node:test';
import { chromium, expect } from '@playwright/test';
import JSZip from 'jszip';

const episode = JSON.parse(readFileSync(new URL('../../src/features/mim-studio/prepared-episode.json', import.meta.url), 'utf8'));
const lessonId = '00000000-0000-4000-8000-000000000001';
const videoId = '00000000-0000-4000-8000-000000000002';
const source = { id: videoId, title: 'شرح التحولات الكبرى', sourceRevision: 1,
  chapters: episode.scenes.flatMap(scene => scene.sourceChapterIds).map(id => ({ id, title: 'فصل من الشرح', summary: 'ملخص الفصل', startTime: 0, endTime: 300 })) };

async function openStudio(page, matches = true) {
  const user = { id: '00000000-0000-4000-8000-000000000003', fullName: 'أدمن الاختبار', roles: ['Admin'], permissions: ['content.manage'],
    allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
  await page.addInitScript(user => {
    localStorage.setItem('accessToken', 'synthetic-test-token'); localStorage.setItem('user', JSON.stringify(user));
  }, user);
  const saves = [];
  let snapshot = null;
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
    let response = [];
    if (path === '/auth/session') response = { user, authorizationVersion: 1 };
    if (path.includes('/cockpit')) response = { lessonId, title: 'المحاضرة السادسة: التحولات الكبرى في مصر خلال العصر الوسيط', summary: '', internalCode: 'L-6',
      order: 1, price: 0, archiveMode: 'None', videos: [], resources: [], homework: [], commentsSummary: { pending: 0, total: 0 } };
    if (path === `/admin/mim-studio/lessons/${lessonId}/sources`) response = [{ ...source, chapters: matches ? source.chapters : [] }];
    if (path === '/admin/mim-studio/connection') response = { connected: false, configured: true, endpoint: 'https://mcp.higgsfield.ai/mcp' };
    if (path === `/admin/mim-studio/lessons/${lessonId}`) {
      if (route.request().method() === 'PUT') {
        const saved = route.request().postDataJSON(); saves.push(saved);
        snapshot = { ...saved, version: '00000000-0000-4000-8000-000000000004', stale: false, updatedAt: new Date().toISOString() };
      }
      response = snapshot;
    }
    await route.fulfill({ json: { success: true, data: response } });
  });
  await page.goto(`http://127.0.0.1:8740/admin/content/lessons/${lessonId}?tab=mim-studio`);
  return saves;
}

test('lesson storyboard edits survive tab changes and save with the lesson source (synthetic API)', { timeout: 90000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
    const saves = await openStudio(page);
    await expect(page.getByRole('heading', { name: 'استوديو ميم', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'ربط حساب Higgsfield', exact: true })).toBeDisabled();
    await page.getByRole('button', { name: 'تعديل الكادرات', exact: true }).click();
    await page.getByLabel('الحوار', { exact: true }).first().fill('ميم: «بابا، فهمني الحكاية!»');
    await page.getByRole('button', { name: 'إنهاء التعديل', exact: true }).click();
    await page.getByRole('tab', { name: 'نظرة عامة', exact: true }).click();
    await page.getByRole('tab', { name: 'استوديو ميم', exact: true }).click();
    await expect(page.getByText('ميم: «بابا، فهمني الحكاية!»', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'حفظ الاسكربت', exact: true }).click();
    await expect(page.getByText('محفوظ في الحصة', { exact: true })).toBeVisible();
    assert.equal(saves.length, 1); assert.equal(saves[0].sourceVideoId, videoId);
    assert.equal(saves[0].document.scenes[0].shots[0].dialogue, 'ميم: «بابا، فهمني الحكاية!»');
    await expect(page.getByRole('button', { name: 'ربط حساب Higgsfield', exact: true })).toBeEnabled();
    const downloadStarted = page.waitForEvent('download');
    await page.getByRole('button', { name: 'تنزيل الاسكربت والشيتين', exact: true }).click();
    const download = await downloadStarted;
    const archive = await JSZip.loadAsync(readFileSync(await download.path()));
    assert.equal(download.suggestedFilename(), 'meem-papa-nader-video-package.zip');
    assert.deepEqual(await archive.file('01-meem-character-sheet.png').async('nodebuffer'), readFileSync('public/mim-studio/meem-character-sheet.png'));
    assert.deepEqual(await archive.file('02-papa-nader-character-sheet.png').async('nodebuffer'), readFileSync('public/mim-studio/papa-nader-character-sheet.png'));
    assert.match(await archive.file('scene-1-prompt.txt').async('string'), /بابا، فهمني الحكاية/);
    mkdirSync('../artifacts/mim-studio', { recursive: true });
    await page.screenshot({ path: '../artifacts/mim-studio/desktop.png', fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    await expect.poll(() => page.locator('#main-content').evaluate(element => element.getBoundingClientRect().width)).toBeGreaterThan(350);
    await page.getByRole('heading', { name: 'استوديو ميم', exact: true }).scrollIntoViewIfNeeded();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
    await page.screenshot({ path: '../artifacts/mim-studio/mobile.png', fullPage: true });
  } finally { await browser.close(); }
});

test('unrelated lessons do not display the prepared history episode (synthetic API)', { timeout: 90000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const page = await browser.newPage(); await openStudio(page, false);
    await expect(page.getByRole('heading', { name: 'الحصة دي لسه مالهاش اسكربت محفوظ' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'حفظ الاسكربت', exact: true })).toHaveCount(0);
  } finally { await browser.close(); }
});

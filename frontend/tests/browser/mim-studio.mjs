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

async function openStudio(page, matches = true, availableVideos = null) {
  const user = { id: '00000000-0000-4000-8000-000000000003', fullName: 'أدمن الاختبار', roles: ['Admin'], permissions: ['content.manage'],
    allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
  await page.addInitScript(user => {
    localStorage.setItem('accessToken', 'synthetic-test-token'); localStorage.setItem('user', JSON.stringify(user));
  }, user);
  const saves = [];
  let snapshot = null;
  let video = null;
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
    let response = [];
    if (path === '/auth/session') response = { user, authorizationVersion: 1 };
    if (path.includes('/cockpit')) response = { lessonId, title: 'المحاضرة السادسة: التحولات الكبرى في مصر خلال العصر الوسيط', summary: '', internalCode: 'L-6',
      order: 1, price: 0, archiveMode: 'None', videos: [], resources: [], homework: [], commentsSummary: { pending: 0, total: 0 } };
    if (path === `/admin/mim-studio/lessons/${lessonId}/sources`) response = availableVideos ?? [{ ...source, chapters: matches ? source.chapters : [] }];
    if (path.endsWith('/scenes/next')) {
      const request = route.request().postDataJSON();
      const scenes = [...(snapshot?.document.scenes ?? []), { ...structuredClone(episode.scenes[request.expectedSceneCount]), sourceChapterIds: [] }];
      snapshot = { version: `version-${scenes.length}`, sourceVideoId: null, sourceRevision: 0, stale: false, generating: false,
        document: { ...episode, scenes, sourceText: request.sourceText } };
      response = snapshot;
    }
    if (path.endsWith('/video/quote')) {
      video = { model:route.request().postDataJSON().model, version: 'quote-1', state: 'quoted', quote: '١٠ كريديت', expiresAt: new Date(Date.now()+300000).toISOString(), urls: [], jobId: null };
      response = video;
    }
    if (path.endsWith('/video')) {
      if (route.request().method() === 'POST') { saves.push({ videoSubmission: true }); video = { ...video, state: 'running', jobId: '00000000-0000-4000-8000-000000000008' }; }
      response = video;
    }
    if (path === '/admin/mim-studio/video-models') response = [{ id:'wan3_0_prime', name:'Wan 3.0 Prime' }, { id:'seedance_2_5', name:'Seedance 2.5' }];
    if (path === '/admin/mim-studio/connection') response = { connected: !matches, configured: true, endpoint: 'https://mcp.higgsfield.ai/mcp' };
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
    await expect(page.getByRole('heading', { name: 'المشهد الأول هيظهر هنا' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'حفظ الاسكربت', exact: true })).toHaveCount(0);
  } finally { await browser.close(); }
});


test('empty lesson writes one scene per click and video waits for explicit cost approval (synthetic API)', { timeout: 90000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const submissions = await openStudio(page, false);
    const first = page.getByRole('button', { name: 'كتابة المشهد الأول', exact: true });
    await expect(first).toBeDisabled();
    await page.getByRole('radio', { name:'إدخال نص يدويًا' }).check();
    await page.getByLabel('نص شرح الحصة', { exact: true }).fill('شرح تفصيلي للحصة ومفاهيمها وأمثلتها، يستند إليه الكاتب في إعداد المشاهد دون اختلاق معلومات جديدة. '.repeat(4));
    await first.click();
    await expect(page.getByRole('button', { name: 'كتابة المشهد التالي (2 من ٤)', exact: true })).toBeVisible();
    await expect(page.getByText('1 من ٤ مشاهد محفوظة. راجع الحوار والحركة قبل المتابعة.', { exact:true })).toBeVisible();
    assert.equal(submissions.length, 0);
    await page.getByRole('button', { name: 'عرض تكلفة هذا المشهد', exact:true }).click();
    const approve = page.getByRole('button', { name: 'توليد هذا المشهد وخصم التكلفة المعروضة', exact:true });
    await expect(approve).toBeEnabled();
    assert.equal(submissions.length, 0);
    await page.getByLabel('موديل توليد الفيديو', { exact:true }).selectOption('seedance_2_5');
    await expect(approve).toHaveCount(0);
    await page.getByRole('button', { name:'تحديث تكلفة هذا المشهد', exact:true }).click();
    await expect(approve).toBeEnabled();
    await approve.click();
    await expect(page.getByText('المشهد قيد التوليد على Higgsfield.', { exact:false })).toBeVisible();
    assert.equal(submissions.length, 1);
    await page.getByRole('button', { name: 'كتابة المشهد التالي (2 من ٤)', exact: true }).click();
    await expect(page.getByRole('button', { name: 'كتابة المشهد التالي (3 من ٤)', exact: true })).toBeVisible();
    assert.equal(submissions.length, 1);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.getByRole('heading', { name: 'استوديو ميم', exact:true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: '../artifacts/mim-studio/scene-generation/mobile.png', fullPage:true });
  } finally { await browser.close(); }
});


test('admin selects a video and Gemini receives that video identity, not another source (synthetic API)', { timeout: 90000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const page = await browser.newPage();
    const chosen = { ...source, title:'فيديو الشرح المختار', chapters:[{ id:'00000000-0000-4000-8000-000000000099', title:'مراحل دورة الماء', summary:'تتبخر المياه ثم تتكثف وتسقط الأمطار.', startTime:0, endTime:120 }] };
    await openStudio(page, false, [{ ...chosen, id:'00000000-0000-4000-8000-000000000098', title:'فيديو بلا تحليل', chapters:[] }, chosen]);
    const first = page.getByRole('button', { name:'كتابة المشهد الأول', exact:true });
    await expect(first).toBeDisabled();
    await expect(page.getByLabel('موديل توليد الفيديو', { exact:true })).toHaveValue('wan3_0_prime');
    await page.getByLabel('فيديو الشرح', { exact:true }).selectOption(videoId);
    await page.getByText('عرض الملخصات اللي جيمناي هيستخدمها (1)', { exact:true }).click();
    await expect(page.getByText('تتبخر المياه ثم تتكثف وتسقط الأمطار.', { exact:true })).toBeVisible();
    const sent = page.waitForRequest(request => request.url().endsWith('/scenes/next'));
    await first.click();
    const request = (await sent).postDataJSON();
    assert.equal(request.sourceVideoId, videoId);
    assert.equal(request.sourceText, null);
    await expect(page.getByRole('button', { name:'كتابة المشهد التالي (2 من ٤)', exact:true })).toBeVisible();
  } finally { await browser.close(); }
});

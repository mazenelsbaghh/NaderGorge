import { installAuthAndGoto } from './e2e-contract-helpers';
import { expect, test } from '@playwright/test';
import { embedSelector, json, openLesson } from '../fixtures/lesson-playback';
import type { LearningSnapshot, LearningEntry } from '../../src/services/video-learning-service';

const snapshot = (): LearningSnapshot => ({
  version: '96000000-0000-0000-0000-000000000500', sourceRevision: 0, stale: false,
  document: { tools: { questions: true, understanding: true, askTeacher: true, notes: true, bookmarks: true,
    timeline: true, cards: true, glossary: true, experiments: true, mastery: true, review: true,
    aiAuthoring: false, aiTutor: true, aiDailyLimit: 5, chapterAids: true }, activities: [{
    id: '96000000-0000-0000-0000-000000000501', kind: 'question', placement: 'moment', seconds: 30, endSeconds: 30,
    title: 'اختار ناتج الجمع', body: '٢ + ٢', answer: '', concept: 'الجمع', options: ['٣', '٤'], correctOption: null,
    required: true, questionBankId: null, experiment: 'linear', factor: 1, offset: 0, minimum: 0, maximum: 10,
  }] }, entries: [], density: [],
});

async function learningPage(page: Parameters<typeof openLesson>[0]) {
  const data = snapshot();
  let saved: LearningEntry | undefined;
  const playback = await openLesson(page, async () => {
    await page.route('**/api/video-learning/*', route => json(route, data));
    await page.route('**/api/video-learning/*/entries', route => {
      const input = route.request().postDataJSON();
      saved = { ...input, correct: input.kind === 'answer' ? input.text === '1' : null, commentId: null };
      data.entries.unshift(saved!);
      return json(route, { entry: saved, explanation: input.kind === 'answer' ? '٢ + ٢ يساوي ٤' : '' });
    });
  });
  const clock = async (currentTime: number) => page.frameLocator(embedSelector).locator('body').evaluate((_, time) => {
    parent.postMessage({ source: 'video-embed', type: 'timeUpdate', data: { currentTime: time, duration: 600 } }, location.origin);
  }, currentTime);
  return { ...playback, data, clock, saved: () => saved };
}

test('required question pauses at its timestamp, grades remotely and retains answer after refresh', async ({ page }) => {
  const lesson = await learningPage(page);
  await lesson.clock(31);
  const gate = page.getByRole('region', { name: 'سؤال مطلوب لاستكمال الفيديو' });
  await expect(gate).toBeVisible();
  await gate.getByText('٤', { exact: true }).click();
  await gate.getByRole('button', { name: 'تأكيد الإجابة' }).click();
  await expect(gate).toHaveCount(0);
  await expect(page.getByText('إجابة صحيحة 💡')).toBeVisible();
  expect(lesson.saved()?.activityId).toBe(lesson.data.document.activities[0].id);
  await page.reload();
  await expect(page.getByRole('region', { name: 'سؤال مطلوب لاستكمال الفيديو' })).toHaveCount(0);
});

test('student saves a timestamped private note and seeks back using its entry', async ({ page }) => {
  const lesson = await learningPage(page);
  await lesson.clock(12);
  await page.getByLabel('عنوان اللحظة').fill('قانون مهم');
  await page.getByLabel('ملاحظتي', { exact: true }).fill('أراجع جمع العددين');
  await page.getByRole('button', { name: 'حفظ الملاحظة', exact: true }).click();
  await expect(page.getByText('أراجع جمع العددين', { exact: true })).toBeVisible();
  expect(lesson.saved()?.seconds).toBe(12);
  await page.getByRole('button', { name: 'قانون مهم · 0:12' }).click();
  await expect(page.getByText('الحفظ عند 0:12.', { exact: false })).toBeVisible();
  await page.screenshot({ path: '/tmp/video-learning-student.png', fullPage: true });
});

test('single tap exposes quarter-step speeds and right double tap seeks ten seconds', async ({ page }) => {
  const lesson = await learningPage(page);
  await page.frameLocator(embedSelector).locator('body').evaluate(() => {
    parent.postMessage({ source: 'video-embed', type: 'ready', data: { duration: 600, provider: 'youtube' } }, location.origin);
  });
  await lesson.clock(10);
  const surface = page.getByRole('region', { name: 'مشغل الفيديو', exact: true });
  await surface.scrollIntoViewIfNeeded();
  const box = (await surface.boundingBox())!;
  const x = box.x + box.width * 0.80, y = box.y + box.height * 0.25;
  const speed = page.getByRole('combobox', { name: 'سرعة التشغيل', exact: true });
  // Hide through the explicit control, then a single surface tap should expose controls again.
  const hide = page.getByRole('button', { name: 'إخفاء عناصر التحكم' });
  if (await hide.isVisible()) await hide.click();
  await page.mouse.click(x, y);
  await expect(speed).toBeVisible();
  await speed.selectOption('1.25');
  await expect(speed).toHaveValue('1.25');
  await speed.selectOption('1.75');
  await expect(speed).toHaveValue('1.75');
  await page.mouse.dblclick(x, y, { delay: 80 });
  await expect(page.getByText('+10 ث', { exact: true })).toBeVisible();
});

test('chapter aids stay inside the player and iPhone opens them only on request', async ({ page, isMobile }) => {
  const lesson = await learningPage(page);
  const chapters = [
    { id: 'chapter-1', title: 'الفصل الأول', startTime: 0, endTime: 10, summaryText: 'مقدمة الجمع', order: 0 },
    { id: 'chapter-2', title: 'الفصل الثاني', startTime: 10, endTime: 25, summaryText: 'شرح الجمع', order: 1, mindmapImageUrl: '/learning-map-test.svg' },
  ];
  await page.route('**/learning-map-test.svg', route => route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" width="300" height="150"><rect width="300" height="150" fill="#eef1f4"/><text x="90" y="80" font-size="30">2 + 2 = 4</text></svg>' }));
  lesson.updateLesson({ ...lesson.lesson, videos: lesson.lesson.videos.map((v, i) => i === 1 ? { ...v, chapters } : v) });
  lesson.notify();
  const panel = page.getByRole('complementary', { name: 'فصول الفيديو والخريطة الذهنية' });
  if (isMobile) {
    await expect(panel).toHaveCount(0);
  } else {
    await expect(panel).toBeVisible();
    await panel.getByRole('button', { name: 'إغلاق معلومات الفصل' }).click();
  }
  await lesson.clock(12);
  if (isMobile) {
    await expect(panel).toHaveCount(0);
    const surface = page.getByRole('region', { name: 'مشغل الفيديو', exact: true });
    await surface.scrollIntoViewIfNeeded();
    await surface.focus();
    await page.getByRole('button', { name: 'الخريطة الذهنية', exact: true }).click();
  }
  await expect(panel.getByRole('heading', { name: 'الفصل الثاني' })).toBeVisible();
  const inside = await panel.evaluate(element => !!element.closest('.secure-video-fullscreen-surface'));
  expect(inside).toBe(true);
  await expect(panel.getByRole('img', { name: 'الخريطة الذهنية: الفصل الثاني' })).toBeVisible();
  await page.getByRole('region', { name: 'مشغل الفيديو', exact: true }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: `/tmp/video-learning-chapters-${isMobile ? 'mobile' : 'desktop'}.png` });
});

test('admin lesson editor publishes selected tools and an activity with timing', async ({ page, baseURL }) => {
  const lessonId = '96000000-0000-0000-0000-000000000001';
  const videoId = '96000000-0000-0000-0000-000000000012';
  const user = { id: '96000000-0000-0000-0000-000000000299', fullName: 'أدمن الاختبار', roles: ['Admin'], permissions: ['content', 'students'], allowedDomains: ['admin'], profileComplete: true, authorizationVersion: 1 };
  const data = snapshot(); data.document.activities = []; data.document.tools.cards = false;
  let published: LearningSnapshot['document'] | undefined;
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/auth/session')) return json(route, { user, authorizationVersion: 1 });
    if (path.endsWith('/teacher/context')) return json(route, { permissions: ['content', 'students'], isOwner: true });
    if (path.endsWith(`/admin/lessons/${lessonId}/cockpit`)) return json(route, { lessonId, title: 'حصة تفاعلية', summary: 'مراجعة الجمع', order: 1, price: 0, archiveMode: 'None', videos: [{ id: videoId, title: 'فيديو الجمع', archiveMode: 'None' }], resources: [], homework: [] });
    if (path.endsWith(`/video-learning/${videoId}/author`)) {
      if (route.request().method() === 'PUT') { published = route.request().postDataJSON().document; data.document = published!; }
      return json(route, data);
    }
    if (path.endsWith('/public/settings')) return route.fulfill({ json: { maintenanceMode: false } });
    return json(route, []);
  });
  const url = new URL(`/admin/content/lessons/${lessonId}`, baseURL); url.hostname = 'admin.lvh.me';
  await installAuthAndGoto(page, 'synthetic-admin-token', user, url.toString());
  await page.getByRole('tab', { name: 'التفاعل والمراجعة', exact: true }).click();
  await page.getByLabel('كروت المراجعة', { exact: true }).check();
  await page.getByRole('button', { name: 'إضافة نشاط', exact: true }).click();
  await page.getByLabel('نوع النشاط').selectOption('card');
  await page.getByLabel('العنوان أو نص السؤال').fill('مراجعة الجمع');
  await page.getByLabel('التوقيت بالثواني').fill('12');
  await page.getByLabel('الإجابة أو المعنى والمثال').fill('٢ + ٢ = ٤');
  await page.getByRole('button', { name: 'حفظ واعتماد التفاعلات', exact: true }).click();
  await expect(page.getByText('تم اعتماد إعدادات التفاعل والأنشطة.', { exact: true })).toBeVisible();
  expect(published?.tools.cards).toBe(true);
  expect(published?.activities[0]).toMatchObject({ kind: 'card', seconds: 12, endSeconds: 12, answer: '٢ + ٢ = ٤' });
  await page.screenshot({ path: '/tmp/video-learning-author.png' });
});


test('disabled tools leave no student notebook or automatic chapter panel', async ({ page }) => {
  const lesson = await learningPage(page);
  const tools = lesson.data.document.tools;
  for (const key of Object.keys(tools) as (keyof typeof tools)[]) {
    if (key !== 'aiDailyLimit') tools[key] = false;
  }
  lesson.data.document.activities = [];
  lesson.updateLesson({ ...lesson.lesson, videos: lesson.lesson.videos.map((v, i) => i === 1 ? { ...v, chapters: [{ id: 'chapter-1', title: 'فصل مغلق', startTime: 0, endTime: 100, summaryText: 'ملخص', order: 0 }] } : v) });
  await page.reload();
  await expect(page.getByRole('region', { name: 'مشغل الفيديو', exact: true })).toBeVisible();
  await lesson.clock(12);
  await expect(page.getByRole('region', { name: 'أدوات التعلم' })).toHaveCount(0);
  await expect(page.getByRole('complementary', { name: 'فصول الفيديو والخريطة الذهنية' })).toHaveCount(0);
});

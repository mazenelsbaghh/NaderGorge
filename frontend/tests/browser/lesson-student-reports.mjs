import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { chromium } from '@playwright/test';

const origin = process.env.LEARNING_REPORT_TEST_ORIGIN ?? 'http://admin.lvh.me:8771';
const teacherId = 'b913ac03-5369-4d17-91d3-1da2327d0789';
const packageId = 'b913ac03-5369-4d17-91d3-1da2327d0781';
const firstLesson = 'b913ac03-5369-4d17-91d3-1da2327d0782';
const secondLesson = 'b913ac03-5369-4d17-91d3-1da2327d0783';
const students = [{ id: 'student-1', name: 'أحمد محمد', phone: '01000000001' }, { id: 'student-2', name: 'أحمد محمد', phone: '01000000002' }];
const choices = { teachers: [{ id: teacherId, name: 'نادر جورج' }], packages: [{ id: packageId, name: 'تاريخ ثانية بكالوريا' }],
  lessons: [{ id: firstLesson, title: 'الحصة الأولى', termTitle: 'الترم الأول' }, { id: secondLesson, title: 'الحصة الثانية', termTitle: 'الترم الأول' }] };
const row = { studentId: students[0].id, studentName: students[0].name, phone: students[0].phone, parentPhone: null,
  grade: 'SecondBaccalaureate', stage: 'Baccalaureate', lessonId: firstLesson, lessonTitle: 'الحصة الأولى', packageName: choices.packages[0].name,
  state: 'Started', totalVideos: 2, completedVideos: 1, progressPercentage: 50, availableSinceUtc: null, daysWaiting: null,
  availabilitySource: 'Unknown', lastWatchedAtUtc: null, videos: [] };
const parts = [{ id: 'video-1', title: 'شرح الحصة', state: 'Completed', progressPercentage: 100, lastWatchedAtUtc: null },
  { id: 'video-2', title: 'تطبيقات لم يشاهدها', state: 'NotStarted', progressPercentage: 0, lastWatchedAtUtc: null }];

async function fixture(browser, audience, width) {
  const page = await browser.newPage({ viewport: { width, height: 950 }, acceptDownloads: true });
  const user = { id: 'report-owner', fullName: 'اختبار التقارير', roles: [audience === 'admin' ? 'Admin' : 'Teacher'], permissions: ['reports.manage'], allowedDomains: [audience], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
  await page.addInitScript(user => { localStorage.setItem('accessToken', 'synthetic-report-token'); localStorage.setItem('user', JSON.stringify(user)); }, user);
  const reads = [], exports = [];
  let failExport = true;
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/auth/session')) return route.fulfill({ json: { success: true, data: { user, authorizationVersion: 1 } } });
    if (path.endsWith('/teacher/context')) return route.fulfill({ json: { success: true, data: { isOwner: true, permissionKeys: ['reports'] } } });
    if (path.endsWith('/reports/learning/options')) return route.fulfill({ json: { success: true, data: choices } });
    if (path.endsWith('/reports/learning/students')) return route.fulfill({ json: { success: true, data: { students, hasMore: false } } });
    if (path.endsWith('/reports/learning/export')) {
      exports.push(route.request().postDataJSON());
      if (failExport) { failExport = false; return route.fulfill({ status: 503, json: { message: 'تعذر تجهيز التقرير الآن' } }); }
      return route.fulfill({ contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', body: 'synthetic spreadsheet transport fixture' });
    }
    if (path.endsWith('/reports/learning')) {
      const filter = route.request().postDataJSON(); reads.push(filter);
      const rows = filter.studentId ? [{ ...row, studentId: students[1].id, phone: students[1].phone, videos: parts },
        { ...row, studentId: students[1].id, phone: students[1].phone, lessonId: secondLesson, lessonTitle: 'الحصة الثانية', state: 'NotStarted', videos: [{ ...parts[1], id: 'video-3', title: 'شرح الحصة الثانية' }] }]
        : [{ ...row, lessonId: filter.lessonIds[0], lessonTitle: filter.lessonIds[0] === firstLesson ? 'الحصة الأولى' : 'الحصة الثانية', state: filter.attendance === 'absent' ? 'NotStarted' : 'Started' }];
      return route.fulfill({ json: { success: true, data: { rows, totalRows: filter.studentId ? 2 : 21, totalStudents: filter.studentId ? 1 : 21,
        completed: 0, started: filter.attendance === 'absent' ? 0 : 1, notStarted: 1, overdue: 0, unknownAvailability: 0, playbackDatedAvailability: 0,
        page: filter.page, pageSize: 20, generatedAtUtc: '2026-10-28T12:00:00Z' } } });
    }
    return route.fulfill({ json: { success: true, data: [] } });
  });
  await page.goto(`${origin.replace('admin.lvh.me', `${audience}.lvh.me`)}/${audience}/reports`);
  await page.getByRole('heading', { name: 'تقارير الحصص والطلاب', exact: true }).waitFor();
  return { page, reads, exports };
}

for (const audience of ['admin', 'teacher']) for (const width of [390, 1280]) {
  test(`${audience} selects a lesson or an exact student and exports the applied report at ${width}px`, { timeout: 120000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const { page, reads, exports } = await fixture(browser, audience, width);
      const run = page.getByRole('button', { name: 'عرض التقرير', exact: true });
      const download = page.getByRole('button', { name: 'تنزيل التقرير إكسيل', exact: true });
      assert.equal(await run.isDisabled(), true);
      if (audience === 'admin') await page.getByLabel('المدرس', { exact: true }).selectOption(teacherId);
      else assert.equal(await page.getByLabel('المدرس', { exact: true }).count(), 0);
      await page.getByLabel('الكورس', { exact: true }).selectOption(packageId);
      await page.getByLabel('الحصة', { exact: true }).selectOption(firstLesson);
      await page.getByLabel('الحضور', { exact: true }).selectOption('present');
      await run.click();
      await page.getByRole('heading', { name: 'كشف الحضور والغياب', exact: true }).waitFor();
      assert.deepEqual(reads.at(-1).lessonIds, [firstLesson]);
      assert.equal(reads.at(-1).attendance, 'present');
      const table = page.getByRole('table', { name: 'حضور وغياب الطلاب في الحصة المحددة' });
      assert.equal(await table.getByRole('cell', { name: 'حاضر', exact: true }).count(), 1);
      await page.getByRole('button', { name: 'التالي', exact: true }).click();
      await page.getByText('صفحة 2 من 2', { exact: true }).waitFor();
      await download.click();
      await page.getByRole('alert').filter({ hasText: 'تعذر تجهيز التقرير الآن' }).waitFor();
      const firstDownload = page.waitForEvent('download'); await download.click();
      assert.match((await firstDownload).suggestedFilename(), /تقرير-الحصص-والطلاب\.xlsx$/);
      assert.deepEqual(exports.at(-1), reads.at(-1));
      await page.getByLabel('الحصة', { exact: true }).selectOption(secondLesson);
      assert.equal(await download.isDisabled(), true);
      assert.equal(await page.getByRole('table').count(), 0);
      await page.getByLabel('الحضور', { exact: true }).selectOption('absent');
      await run.click(); await table.waitFor();
      assert.equal(await table.getByRole('cell', { name: 'غائب', exact: true }).count(), 1);
      assert.deepEqual(reads.at(-1).lessonIds, [secondLesson]);

      await page.getByRole('button', { name: 'مشاهدة طالب', exact: true }).click();
      await page.getByLabel('ابحث عن طالب', { exact: true }).fill('أحمد');
      await page.getByRole('button', { name: /أحمد محمد.*01000000002/ }).click();
      await run.click();
      await page.getByRole('heading', { name: /مشاهدة الطالب/ }).waitFor();
      assert.equal(reads.at(-1).studentId, students[1].id);
      assert.deepEqual(reads.at(-1).lessonIds, []);
      await page.getByText('شرح الحصة', { exact: true }).waitFor();
      await page.getByText('تطبيقات لم يشاهدها', { exact: true }).waitFor();
      await page.getByText('شرح الحصة الثانية', { exact: true }).waitFor();
      const studentDownload = page.waitForEvent('download'); await download.click(); await studentDownload;
      assert.equal(exports.at(-1).studentId, students[1].id);
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1), false);
      const screenshots = process.env.LEARNING_REPORT_SCREENSHOTS ?? '/tmp/codex-lesson-reports-screenshots';
      await mkdir(screenshots, { recursive: true });
      await page.screenshot({ path: `${screenshots}/${audience}-${width}.png`, fullPage: true, animations: 'disabled' });
      await page.getByLabel('الكورس', { exact: true }).selectOption('');
      assert.equal(await run.isDisabled(), true);
      assert.equal(await download.isDisabled(), true);
      assert.equal(await page.getByText('شرح الحصة', { exact: true }).count(), 0);
    } finally { await browser.close(); }
  });
}

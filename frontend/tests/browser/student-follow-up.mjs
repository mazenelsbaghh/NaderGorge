import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { chromium } from '@playwright/test';

const origin = process.env.FOLLOW_UP_TEST_ORIGIN ?? 'http://admin.lvh.me:8771';
const teacher = 'b913ac03-5369-4d17-91d3-1da2327d0789', course = 'b913ac03-5369-4d17-91d3-1da2327d0781';
const first = 'b913ac03-5369-4d17-91d3-1da2327d0782', second = 'b913ac03-5369-4d17-91d3-1da2327d0783';
const options = { teachers: [{ id: teacher, name: 'نادر جورج' }], packages: [{ id: course, name: 'التاريخ ثانية بكالوريا', grades: [{ stage: 1, grade: 3 }] }, { id: 'secondary-course', name: 'تاريخ أولى ثانوي', grades: [{ stage: 0, grade: 0 }] }], lessons: [{ id: first, title: 'الحصة الأولى', termTitle: 'الترم الأول', order: 1 }, { id: second, title: 'الحصة الثانية', termTitle: 'الترم الأول', order: 2 }] };
const row = { studentId: 'student-1', studentName: 'أحمد محمد', phone: '01000000001', parentPhone: '01000000002', grade: 'SecondBaccalaureate', stage: 'Baccalaureate', lessonId: first, lessonTitle: 'الحصة الأولى', packageName: options.packages[0].name, state: 'NotStarted', totalVideos: 1, completedVideos: 0, progressPercentage: 0, availableSinceUtc: '2026-10-10T12:00:00Z', daysWaiting: 8, availabilitySource: 'VideoOpened', lastWatchedAtUtc: null };
async function fixture(browser, audience, width) {
  const page = await browser.newPage({ viewport: { width, height: 950 }, acceptDownloads: true });
  const user = { id: 'test-owner', fullName: 'اختبار المتابعة', roles: [audience === 'admin' ? 'Admin' : 'Teacher'], permissions: ['users.manage'], allowedDomains: [audience], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
  await page.addInitScript(user => { localStorage.setItem('accessToken', 'test-token'); localStorage.setItem('user', JSON.stringify(user)); }, user);
  const reads = [], exports = []; let failExport = true;
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/auth/session')) return route.fulfill({ json: { success: true, data: { user, authorizationVersion: 1 } } });
    if (path.endsWith('/teacher/context')) return route.fulfill({ json: { success: true, data: { isOwner: true, permissionKeys: ['students'] } } });
    if (path.endsWith('/student-follow-up/options')) return route.fulfill({ json: { success: true, data: options } });
    if (path.endsWith('/student-follow-up/export')) {
      exports.push(route.request().postDataJSON());
      if (failExport) { failExport = false; return route.fulfill({ status: 503, json: { message: 'تعذر تجهيز كشف المتابعة' } }); }
      return route.fulfill({ contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', body: 'PK synthetic download fixture' });
    }
    if (path.endsWith('/student-follow-up')) {
      const filter = route.request().postDataJSON(); reads.push(filter);
      return route.fulfill({ json: { success: true, data: { rows: [filter.page === 2 ? { ...row, studentId: 'student-2', studentName: 'منى عادل' } : row], totalRows: 21, totalStudents: 21, completed: 0, started: 0, notStarted: 21, overdue: 21, unknownAvailability: 0, playbackDatedAvailability: 0, page: filter.page, pageSize: 20, generatedAtUtc: '2026-10-18T12:00:00Z' } } });
    }
    return route.fulfill({ json: { success: true, data: [] } });
  });
  await page.goto(`${origin.replace('admin.lvh.me', `${audience}.lvh.me`)}/${audience}/student-follow-up`);
  await page.getByRole('heading', { name: 'متابعة الطلاب', exact: true }).waitFor();
  return { page, reads, exports };
}
for (const audience of ['admin', 'teacher']) for (const width of [390, 1280]) {
  test(`${audience} filters lectures and grade, pages and exports applied filters at ${width}px`, { timeout: 120000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const { page, reads, exports } = await fixture(browser, audience, width);
      if (audience === 'admin') await page.getByLabel('المدرس', { exact: true }).selectOption(teacher);
      else assert.equal(await page.getByLabel('المدرس', { exact: true }).count(), 0);
      if (audience === 'teacher') {
        assert.equal(await page.getByLabel('الكورس', { exact: true }).isDisabled(), true);
        assert.deepEqual(await page.getByLabel('الصف', { exact: true }).locator('option').allTextContents(), ['اختر الصف', 'الأول الثانوي', 'الثاني بكالوريا']);
      }
      await page.getByLabel('الصف', { exact: true }).selectOption('1:3');
      await page.getByLabel('الكورس', { exact: true }).selectOption(course);
      if (audience === 'teacher') assert.deepEqual(await page.getByLabel('الكورس', { exact: true }).locator('option').allTextContents(), ['اختر الكورس', 'التاريخ ثانية بكالوريا']);
      await page.getByRole('checkbox', { name: /الحصة الأولى/ }).check();
      await page.getByRole('checkbox', { name: /الحصة الثانية/ }).check();
      await page.getByLabel('مدة التأخير', { exact: true }).selectOption('7');
      await page.getByRole('button', { name: 'عرض الطلاب', exact: true }).click();
      await page.getByRole('heading', { name: /نتيجة المتابعة/ }).waitFor();
      assert.equal(reads.length, 1); assert.deepEqual(reads[0].lessonIds, [first, second]);
      assert.deepEqual([reads[0].stage, reads[0].grade, reads[0].minimumDays, reads[0].state], [1, 3, 7, 'NotStarted']);
      const downloadButton = page.getByRole('button', { name: 'تنزيل النتيجة إكسيل', exact: true });
      await downloadButton.click(); await page.getByRole('alert').filter({ hasText: 'تعذر تجهيز كشف المتابعة' }).waitFor();
      const downloaded = page.waitForEvent('download'); await downloadButton.click();
      assert.match((await downloaded).suggestedFilename(), /متابعة-الطلاب\.xlsx$/); assert.deepEqual(exports[1], reads[0]);
      await page.getByLabel('مدة التأخير', { exact: true }).selectOption('14');
      assert.equal(await downloadButton.isDisabled(), true); assert.equal(await page.getByRole('button', { name: /التالي/ }).isDisabled(), true);
      await page.getByRole('button', { name: 'عرض الطلاب', exact: true }).click();
      await page.getByText('صفحة 1 من 2', { exact: true }).waitFor(); assert.equal(reads.at(-1).minimumDays, 14);
      await page.getByRole('button', { name: /التالي/ }).click(); await page.getByText('صفحة 2 من 2', { exact: true }).waitFor();
      assert.equal(reads.at(-1).page, 2);
      const nextDownload = page.waitForEvent('download'); await downloadButton.click(); await nextDownload; assert.deepEqual(exports.at(-1), reads.at(-1));
      if (audience === 'teacher') {
        await page.getByLabel('الصف', { exact: true }).selectOption('0:0');
        assert.deepEqual(await page.getByLabel('الكورس', { exact: true }).locator('option').allTextContents(), ['اختر الكورس', 'تاريخ أولى ثانوي']);
        assert.equal(await page.getByRole('checkbox', { name: /الحصة الأولى/ }).count(), 0);
        assert.equal(await downloadButton.isDisabled(), true);
        assert.equal(await page.getByRole('heading', { name: /نتيجة المتابعة/ }).count(), 0);
        assert.equal(await page.getByRole('button', { name: 'عرض الطلاب', exact: true }).isDisabled(), true);
      }
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1), false);
      await mkdir('../artifacts/production/student-follow-up-grade-filter-20261018/screenshots', { recursive: true });
      await page.screenshot({ path: `../artifacts/production/student-follow-up-grade-filter-20261018/screenshots/${audience}-${width}.png`, fullPage: true });
    } finally { await browser.close(); }
  });
}

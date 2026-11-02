import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { chromium } from '@playwright/test';

const origin = process.env.COURSE_SUMMARY_TEST_ORIGIN ?? 'http://teacher.lvh.me:8771';
const secondaryCourse = 'b913ac03-5369-4d17-91d3-1da2327d0781';
const baccalaureateCourse = 'b913ac03-5369-4d17-91d3-1da2327d0782';
const options = { teachers: [], lessons: [], packages: [
  { id: secondaryCourse, name: 'تاريخ ثانوي', grades: [{ stage: 0, grade: 0 }] },
  { id: baccalaureateCourse, name: 'تاريخ بكالوريا', grades: [{ stage: 1, grade: 3 }] },
] };
const columns = ['اسم الطالب', 'كود الطالب', 'الموبايل', 'ولي الأمر', 'المرحلة', 'الصف', 'المحافظة', 'المنطقة', 'العنوان', 'المدرسة', 'الشعبة', 'موبايل إضافي', 'ولي الأمر الإضافي', 'الأم', 'الجنسية']
  .map((label, index) => ({ key: `identity${index}`, label, type: 'text' }));
const student = Object.fromEntries(columns.map(column => [column.key, 'غير مسجل']));
const report = { columns: [...columns, { key: 'lesson0', label: 'تاريخ ثانوي / الحصة الأولى / المشاهدة', type: 'text' }],
  rows: [{ ...student, id: 'synthetic-student', identity0: 'طالب التقرير', identity2: '01012345678', identity6: 'الإسكندرية', identity8: 'عنوان الطالب', lesson0: 'شاهد' }], totalStudents: 21, page: 1, pageSize: 20 };

for (const width of [390, 1280]) {
  test(`teacher summary combines all grades, previews complete details and exports optional filters at ${width}px (synthetic HTTP)`, { timeout: 120000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width, height: 950 }, acceptDownloads: true });
      const user = { id: 'teacher-summary', fullName: 'مدرس الملخص', roles: ['Teacher'], permissions: [], allowedDomains: ['teacher'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => { localStorage.setItem('accessToken', 'synthetic-summary-token'); localStorage.setItem('user', JSON.stringify(user)); }, user);
      let lookupFails = true, exportFails = true;
      const exports = [], reads = [];
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname;
        if (path.endsWith('/auth/session')) return route.fulfill({ json: { success: true, data: { user, authorizationVersion: 1 } } });
        if (path.endsWith('/teacher/context')) return route.fulfill({ json: { success: true, data: { isOwner: true, permissionKeys: ['reports'] } } });
        if (path.endsWith('/reports/learning/options')) return lookupFails
          ? route.fulfill({ status: 503, json: { message: 'تعذر تحميل الكورسات الآن' } })
          : route.fulfill({ json: { success: true, data: options } });
        if (path.endsWith('/reports/summary')) {
          const selection = route.request().postDataJSON(); reads.push(selection);
          return route.fulfill({ json: { success: true, data: { ...report, page: selection.page } } });
        }
        if (path.endsWith('/reports/summary/export')) {
          exports.push(route.request().postDataJSON());
          if (exportFails) { exportFails = false; return route.fulfill({ status: 503, json: { message: 'تعذر تجهيز الملخص الآن' } }); }
          return route.fulfill({ contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', body: 'synthetic download transport fixture' });
        }
        return route.fulfill({ json: { success: true, data: [] } });
      });
      await page.goto(`${origin}/teacher/reports`);
      const summary = page.getByRole('region', { name: 'ملخص', exact: true });
      const download = summary.getByRole('button', { name: 'تنزيل الداتا إكسيل', exact: true });
      await summary.getByRole('alert').waitFor();
      assert.equal(await download.isDisabled(), true);
      lookupFails = false;
      await summary.getByRole('button', { name: 'إعادة تحميل الكورسات', exact: true }).click();
      await download.waitFor({ state: 'visible' });
      await summary.getByRole('button', { name: 'عرض التقرير', exact: true }).click();
      const results = summary.getByRole('region', { name: 'نتيجة ملخص الطلاب' });
      await results.getByText('الإسكندرية', { exact: true }).filter({ visible: true }).waitFor();
      assert.deepEqual(reads.at(-1), { page: 1 });
      await results.getByRole('button', { name: 'التالي', exact: true }).click();
      await results.getByText('صفحة 2 من 2', { exact: true }).waitFor();
      assert.deepEqual(reads.at(-1), { page: 2 });
      await download.click();
      await summary.getByRole('alert').filter({ hasText: 'تعذر تجهيز الملخص الآن' }).waitFor();
      const allDownload = page.waitForEvent('download'); await download.click();
      assert.equal((await allDownload).suggestedFilename(), 'ملخص-الطلاب.xlsx');
      assert.deepEqual(exports.at(-1), {});
      await summary.getByLabel('المرحلة', { exact: true }).selectOption('0');
      assert.equal(await results.count(), 0);
      assert.deepEqual(await summary.getByLabel('الكورس', { exact: true }).locator('option').allTextContents(), ['كل الكورسات', 'تاريخ ثانوي']);
      await summary.getByLabel('الصف', { exact: true }).selectOption('0');
      await summary.getByLabel('الكورس', { exact: true }).selectOption(secondaryCourse);
      const filteredDownload = page.waitForEvent('download'); await download.click(); await filteredDownload;
      assert.deepEqual(exports.at(-1), { stage: 0, grade: 0, packageId: secondaryCourse });
      await summary.getByLabel('المرحلة', { exact: true }).selectOption('1');
      assert.equal(await summary.getByLabel('الصف', { exact: true }).inputValue(), '');
      assert.equal(await summary.getByLabel('الكورس', { exact: true }).inputValue(), '');
      assert.deepEqual(await summary.getByLabel('الكورس', { exact: true }).locator('option').allTextContents(), ['كل الكورسات', 'تاريخ بكالوريا']);
      await summary.getByRole('button', { name: 'عرض التقرير', exact: true }).click();
      await results.getByText('الإسكندرية', { exact: true }).filter({ visible: true }).waitFor();
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1), false);
      await results.scrollIntoViewIfNeeded();
      await mkdir('/tmp/codex-course-summary-screenshots', { recursive: true });
      await results.screenshot({ path: `/tmp/codex-course-summary-screenshots/teacher-summary-${width}.png`, animations: 'disabled' });
    } finally { await browser.close(); }
  });
}

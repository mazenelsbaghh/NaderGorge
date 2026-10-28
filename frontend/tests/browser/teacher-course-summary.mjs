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

for (const width of [390, 1280]) {
  test(`teacher summary filters courses by stage, recovers from errors and downloads the selection at ${width}px`, { timeout: 120000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width, height: 950 }, acceptDownloads: true });
      const user = { id: 'teacher-summary', fullName: 'مدرس الملخص', roles: ['Teacher'], permissions: [], allowedDomains: ['teacher'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => { localStorage.setItem('accessToken', 'synthetic-summary-token'); localStorage.setItem('user', JSON.stringify(user)); }, user);
      let lookupFails = true, exportFails = true;
      const exports = [];
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname;
        if (path.endsWith('/auth/session')) return route.fulfill({ json: { success: true, data: { user, authorizationVersion: 1 } } });
        if (path.endsWith('/teacher/context')) return route.fulfill({ json: { success: true, data: { isOwner: true, permissionKeys: ['reports'] } } });
        if (path.endsWith('/reports/learning/options')) return lookupFails
          ? route.fulfill({ status: 503, json: { message: 'تعذر تحميل الكورسات الآن' } })
          : route.fulfill({ json: { success: true, data: options } });
        if (path.endsWith('/reports/summary/export')) {
          exports.push(route.request().postDataJSON());
          if (exportFails) { exportFails = false; return route.fulfill({ status: 503, json: { message: 'تعذر تجهيز الملخص الآن' } }); }
          return route.fulfill({ contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', body: 'synthetic download transport fixture' });
        }
        return route.fulfill({ json: { success: true, data: [] } });
      });
      await page.goto(`${origin}/teacher/reports`);
      await page.getByRole('button', { name: 'ملخص', exact: true }).click();
      const summary = page.getByRole('region', { name: 'ملخص', exact: true });
      const download = summary.getByRole('button', { name: 'تنزيل الداتا إكسيل', exact: true });
      await summary.getByRole('alert').waitFor();
      assert.equal(await download.isDisabled(), true);
      lookupFails = false;
      await summary.getByRole('button', { name: 'إعادة تحميل الكورسات', exact: true }).click();
      await summary.getByLabel('المرحلة', { exact: true }).selectOption('0');
      assert.deepEqual(await summary.getByLabel('الكورس', { exact: true }).locator('option').allTextContents(), ['اختر الكورس', 'تاريخ ثانوي']);
      await summary.getByLabel('الكورس', { exact: true }).selectOption(secondaryCourse);
      await download.click();
      await summary.getByRole('alert').filter({ hasText: 'تعذر تجهيز الملخص الآن' }).waitFor();
      const firstDownload = page.waitForEvent('download'); await download.click();
      assert.equal((await firstDownload).suggestedFilename(), 'ملخص-الكورس.xlsx');
      assert.deepEqual(exports.at(-1), { stage: 0, packageId: secondaryCourse });
      await summary.getByText('تم تنزيل ملخص الكورس.', { exact: true }).waitFor();
      await summary.getByLabel('المرحلة', { exact: true }).selectOption('1');
      assert.equal(await download.isDisabled(), true);
      assert.equal(await summary.getByText('تم تنزيل ملخص الكورس.', { exact: true }).count(), 0);
      assert.deepEqual(await summary.getByLabel('الكورس', { exact: true }).locator('option').allTextContents(), ['اختر الكورس', 'تاريخ بكالوريا']);
      await summary.getByLabel('الكورس', { exact: true }).selectOption(baccalaureateCourse);
      const secondDownload = page.waitForEvent('download'); await download.click(); await secondDownload;
      assert.deepEqual(exports.at(-1), { stage: 1, packageId: baccalaureateCourse });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1), false);
      await mkdir('/tmp/codex-course-summary-screenshots', { recursive: true });
      await page.screenshot({ path: `/tmp/codex-course-summary-screenshots/teacher-summary-${width}.png`, fullPage: true, animations: 'disabled' });
    } finally { await browser.close(); }
  });
}

import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { chromium } from '@playwright/test';

const origin = process.env.ASSESSMENT_EXPORT_TEST_ORIGIN ?? 'http://localhost:8738';
const assessmentId = '6d093951-9d26-4199-b203-a9d56f8d06e6';
const productId = 'd7ecf233-c0ed-40a7-b3c0-fda3bfc086c6';
const cases = [
  { audience: 'teacher', kind: 'exam', width: 390 },
  { audience: 'admin', kind: 'exam', width: 1280 },
  { audience: 'teacher', kind: 'homework', width: 1280 },
  { audience: 'admin', kind: 'homework', width: 390 },
  { audience: 'admin', kind: 'exam', width: 1280, publicExam: true },
];

for (const scenario of cases) {
  test(`${scenario.audience} downloads exact ${scenario.publicExam ? 'public ' : ''}${scenario.kind} grades and absence at ${scenario.width}px`, { timeout: 120_000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width: scenario.width, height: 900 }, acceptDownloads: true });
      const user = { id: 'synthetic-export-actor', fullName: 'اختبار الكشف', roles: [scenario.audience === 'admin' ? 'Admin' : 'Teacher'],
        permissions: ['content.manage', 'exams.manage'], allowedDomains: [scenario.audience], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => { localStorage.setItem('accessToken', 'synthetic-export-token'); localStorage.setItem('user', JSON.stringify(user)); }, user);
      const exports = [];
      let rejectFirstExport = true;
      await page.route('**/api/**', async route => {
        const url = new URL(route.request().url());
        if (url.pathname.endsWith('/export')) {
          exports.push(url);
          if (rejectFirstExport) { rejectFirstExport = false; return route.fulfill({ status: 503, json: { message: 'تعذر تجهيز كشف الاختبار، أعد المحاولة.' } }); }
          return route.fulfill({ contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', body: 'PK\x03\x04 synthetic browser download' });
        }
        if (url.pathname.endsWith('/auth/session')) return route.fulfill({ json: { success: true, data: { user, authorizationVersion: 1 } } });
        if (url.pathname.endsWith('/teacher/context')) return route.fulfill({ json: { success: true, data: { isOwner: true, permissionKeys: ['content', 'students', 'publicExams', 'reports'] } } });
        if (url.pathname.includes('/public-exams/') && url.pathname.endsWith('/results')) return route.fulfill({ json: { success: true, data: {
          productId, examId: assessmentId, examTitle: 'نفس اسم الامتحان', isPaid: true, price: 10, attemptCount: 0, passedCount: 0, averageScore: 0, attempts: [], questions: [],
        } } });
        if (url.pathname.includes(assessmentId)) return route.fulfill({ json: { success: true, data: {
          examId: assessmentId, homeworkId: assessmentId, title: 'نفس اسم الامتحان', internalCode: `EXM-${assessmentId}`, description: '',
          totalScore: 10, passingScore: 5, isActive: true, archiveMode: 0, questionCount: 0, attempts: [], submissions: [], questions: [],
        } } });
        return route.fulfill({ json: { success: true, data: [] } });
      });
      const base = scenario.audience === 'teacher' ? '/teacher/packages' : '/admin/content';
      const resource = scenario.kind === 'exam' ? 'exams' : 'homework';
      const route = scenario.publicExam ? `/admin/public-exams/${productId}/results` : `${base}/${resource}/${assessmentId}`;
      await page.goto(`${origin}${route}`);
      const grades = page.getByRole('button', { name: 'تنزيل الدرجات إكسيل', exact: true });
      await grades.click();
      await page.getByText('تعذر تجهيز كشف الاختبار، أعد المحاولة.', { exact: true }).waitFor();
      const gradeDownload = page.waitForEvent('download');
      await grades.click();
      assert.equal((await gradeDownload).suggestedFilename(), `${scenario.kind}-${assessmentId}-grades.xlsx`);
      const absenceDownload = page.waitForEvent('download');
      await page.getByRole('button', { name: 'تنزيل الغياب إكسيل', exact: true }).click();
      assert.equal((await absenceDownload).suggestedFilename(), `${scenario.kind}-${assessmentId}-absent.xlsx`);
      assert.equal(exports.length, 3);
      assert(exports.every(url => url.pathname === `/api/${scenario.audience}/${resource}/${assessmentId}/export`));
      assert.deepEqual(exports.map(url => url.searchParams.get('selection')), ['grades', 'grades', 'absent']);
      assert(exports.every(url => !url.searchParams.has('title') && !url.pathname.includes(productId)));
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1), false);
      const previews = '../artifacts/production/assessment-grade-export-20261122/ui';
      await mkdir(previews, { recursive: true });
      await page.screenshot({ path: `${previews}/${scenario.audience}-${scenario.kind}-${scenario.width}${scenario.publicExam ? '-public' : ''}.png`, fullPage: true, animations: 'disabled' });
    } finally { await browser.close(); }
  });
}

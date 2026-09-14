import assert from 'node:assert/strict';
import test from 'node:test';
import { setTimeout as delay } from 'node:timers/promises';
import { chromium } from '@playwright/test';

test('lesson PDF upload survives a slow response and reports a rejection only once', { timeout: 120000 }, async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    page.setDefaultTimeout(40000);
    const user = { id: 'author', fullName: 'محرر الملفات', roles: ['Admin'], permissions: ['content.manage'], allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
    await page.addInitScript(user => {
      localStorage.setItem('accessToken', 'test-token');
      localStorage.setItem('user', JSON.stringify(user));
    }, user);
    const lesson = { lessonId: 'lesson', title: 'حصة اختبار', summary: '', price: 0, order: 1, archiveMode: 'None', examArchiveMode: 'None', videos: [], resources: [], homework: [], commentsSummary: { total: 0, pending: 0 } };
    let rejectUpload = true;
    const createdResources = [];
    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
      let data = [];
      if (path === '/auth/session') data = { user, authorizationVersion: 1 };
      if (path.endsWith('/cockpit')) data = lesson;
      if (path === '/admin/resources/upload') {
        if (rejectUpload) return route.fulfill({ status: 413, json: { success: false } });
        // Regression: the normal API timeout used to abort uploads after 20 seconds.
        await delay(21000);
        return route.fulfill({ json: { success: true, data: { url: '/protected/resources/test.pdf' } } });
      }
      if (path === '/admin/resources' && route.request().method() === 'POST') {
        createdResources.push(route.request().postDataJSON());
        data = { id: 'resource' };
      }
      await route.fulfill({ json: { success: true, data } });
    });
    await page.goto('http://admin.lvh.me:8738/admin/content/lessons/lesson');
    await page.getByRole('tab', { name: 'المذكرات والملفات', exact: true }).click();
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({ name: 'مذكرة.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.7\nlesson') });
    await page.getByRole('button', { name: 'إضافة الملف', exact: true }).click();
    await page.getByText('حجم الملف أكبر من المسموح. الحد الأقصى 10 ميجابايت.', { exact: true }).waitFor();
    assert.equal(await page.locator('[role="status"]').count(), 1);
    assert.equal(createdResources.length, 0);
    rejectUpload = false;
    await page.getByRole('button', { name: 'إضافة الملف', exact: true }).click();
    await page.getByText('تم إرفاق الملف بنجاح.', { exact: true }).waitFor();
    assert.deepEqual(createdResources, [{ lessonId: 'lesson', title: 'مذكرة', fileUrl: '/protected/resources/test.pdf', resourceType: 'PDF' }]);
    assert.equal(await fileInput.inputValue(), '');
  } finally {
    await browser.close();
  }
});

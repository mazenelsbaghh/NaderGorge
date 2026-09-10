import assert from 'node:assert/strict';
import test from 'node:test';
import { webkit } from '@playwright/test';
import { mkdir } from 'node:fs/promises';

test('admin explicitly retains same-video content or confirms clearing a different source', { timeout: 120000 }, async () => {
  const browser = await webkit.launch();
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    page.setDefaultTimeout(30000);
    const user = { id: 'author', fullName: 'محرر الفيديو', roles: ['Admin'], permissions: ['content.manage'], allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
    await page.addInitScript(user => {
      localStorage.setItem('accessToken', 'test-token');
      localStorage.setItem('user', JSON.stringify(user));
    }, user);
    const video = { id: 'video', internalCode: 'VID-example', title: 'مقدمة التاريخ', provider: 'youtube', url: 'https://youtu.be/dQw4w9WgXcQ', order: 1, maxWatchCount: 3, isActive: true, archiveMode: 'None', videoType: { id: 'type', name: 'شرح', isActive: true }, chapters: [] };
    const lesson = { lessonId: 'lesson', internalCode: 'LES-example', title: 'الحصة الأولى', summary: '', price: 0, order: 1, archiveMode: 'None', examArchiveMode: 'None', videos: [video], resources: [], homework: [], commentsSummary: { total: 0, pending: 0 } };
    const writes = [];
    let failSave = true;
    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
      let data = [];
      if (path === '/auth/session') data = { user, authorizationVersion: 1 };
      if (path.endsWith('/cockpit')) data = lesson;
      if (path.includes('/video-types')) data = [{ id: 'type', name: 'شرح', isActive: true, sortOrder: 1 }];
      if (path === '/admin/videos/video' && route.request().method() === 'PUT') {
        const payload = route.request().postDataJSON();
        writes.push(payload);
        if (failSave) return route.fulfill({ status: 503, json: { success: false, message: 'تعذر الحفظ مؤقتًا' } });
        video.url = payload.urlOrEmbedCode;
      }
      await route.fulfill({ json: { success: true, data } });
    });
    await page.goto(`${process.env.VIDEO_SOURCE_TEST_URL ?? 'http://admin.lvh.me:8738'}/admin/content/lessons/lesson`);
    await page.getByRole('tab', { name: 'الفيديوهات', exact: true }).click();
    await page.getByRole('button', { name: 'تعديل الفيديو', exact: true }).click();
    const form = page.locator('#edit-video-form-video');
    await form.getByPlaceholder('رابط الفيديو', { exact: true }).fill('https://youtu.be/M7lc1UVf-VE');
    assert.equal(await form.getByRole('radio', { name: 'نفس الفيديو، احتفظ بالفصول والترجمة والخرائط' }).isChecked(), true);
    await form.getByRole('button', { name: 'حفظ التعديلات' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'تحديث الرابط والاحتفاظ بالبيانات' }).click();
    await page.getByText('تعذر الحفظ مؤقتًا', { exact: true }).waitFor();
    assert.equal(await dialog.isVisible(), true);
    assert.equal(await form.getByPlaceholder('رابط الفيديو', { exact: true }).inputValue(), 'https://youtu.be/M7lc1UVf-VE');
    failSave = false;
    await dialog.getByRole('button', { name: 'تحديث الرابط والاحتفاظ بالبيانات' }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert.equal(writes.length, 2);
    assert.ok(writes.every(payload => payload.preserveSourceDerivedData === true));
    await page.getByRole('button', { name: 'تعديل الفيديو', exact: true }).click();
    await form.getByPlaceholder('رابط الفيديو', { exact: true }).fill('https://youtu.be/dQw4w9WgXcQ');
    await form.getByRole('radio', { name: 'فيديو مختلف، احذف البيانات المرتبطة بالمصدر القديم' }).check();
    await form.getByRole('button', { name: 'حفظ التعديلات' }).click();
    assert.ok(await dialog.evaluate(element => element.scrollWidth <= element.clientWidth + 1));
    await page.waitForFunction(() => {
      const dialog = document.querySelector('[role="dialog"]');
      return dialog && getComputedStyle(dialog).opacity === '1' && getComputedStyle(dialog.parentElement).opacity === '1';
    });
    await mkdir('../artifacts/video-source-retention', { recursive: true });
    await page.screenshot({ path: '../artifacts/video-source-retention/mobile-confirmation.png' });
    await dialog.getByRole('button', { name: 'استبدال المصدر وحذف البيانات القديمة' }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert.equal(writes.at(-1).preserveSourceDerivedData, false);
  } finally {
    await browser.close();
  }
});

import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { chromium } from '@playwright/test';

for (const width of [390, 1440]) {
  test(`teacher renames lesson and video without exposing source controls (${width}px)`, { timeout: 120000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width, height: 900 } });
      const user = { id: 'teacher', fullName: 'مدرس التجربة', roles: ['Teacher'], permissions: ['content.manage'],
        allowedDomains: ['teacher'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => {
        localStorage.setItem('accessToken', 'test-token');
        localStorage.setItem('user', JSON.stringify(user));
      }, user);
      const lesson = { lessonId: 'lesson', title: 'حصة التاريخ', summary: '', price: 150, order: 1,
        archiveMode: 'Active', resources: [], homework: [], commentsSummary: { pending: 0 }, videos: [
          { id: 'part-one', title: 'البارت الأول', provider: '', url: '', order: 1, maxWatchCount: 3,
            isActive: true, videoType: { name: 'شرح' }, chapters: [], exams: [] },
        ] };
      const changes = [];
      let rejectVideoSave = true;
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
        let data = [];
        if (path === '/auth/session') data = { user, authorizationVersion: 1 };
        if (path === '/teacher/context') data = { isOwner: true, permissionKeys: ['content'] };
        if (path === '/teacher/lessons/lesson/cockpit') data = lesson;
        if (route.request().method() === 'PATCH') {
          const body = route.request().postDataJSON();
          changes.push({ path, body });
          if (path === '/teacher/videos/part-one/title' && rejectVideoSave)
            return route.fulfill({ status: 409, json: { success: false, message: 'تعذر حفظ الاسم الآن' } });
          if (path === '/teacher/lessons/lesson/title') lesson.title = body.title;
          if (path === '/teacher/videos/part-one/title') lesson.videos[0].title = body.title;
        }
        await route.fulfill({ json: { success: true, data } });
      });
      await page.goto('http://teacher.lvh.me:8738/teacher/packages/lessons/lesson');
      await page.getByRole('button', { name: 'تعديل اسم الحصة', exact: true }).click();
      await page.getByLabel('اسم الحصة', { exact: true }).fill('حصة التاريخ الجديدة');
      await page.getByRole('button', { name: 'حفظ الاسم', exact: true }).click();
      await page.getByRole('heading', { name: 'حصة التاريخ الجديدة', exact: true, level: 1 }).waitFor();
      await page.getByRole('tab', { name: 'الفيديوهات', exact: true }).click();
      await page.getByRole('button', { name: 'تعديل اسم الفيديو', exact: true }).click();
      const videoTitle = page.getByLabel('اسم الفيديو', { exact: true });
      await videoTitle.fill('اسم لن يُحفظ');
      await page.getByRole('button', { name: 'إلغاء', exact: true }).click();
      assert.equal(changes.length, 1);
      await page.getByRole('heading', { name: 'البارت الأول', exact: true }).waitFor();
      await page.getByRole('button', { name: 'تعديل اسم الفيديو', exact: true }).click();
      await videoTitle.fill('البارت الأول المعدّل');
      await page.getByRole('button', { name: 'حفظ الاسم', exact: true }).click();
      await page.getByRole('alert').filter({ hasText: 'تعذر حفظ الاسم الآن' }).waitFor();
      assert.equal(await videoTitle.inputValue(), 'البارت الأول المعدّل');
      rejectVideoSave = false;
      await page.getByRole('button', { name: 'حفظ الاسم', exact: true }).click();
      await page.getByRole('heading', { name: 'البارت الأول المعدّل', exact: true }).waitFor();
      for (const { path, body } of changes) {
        assert.ok(/^\/teacher\/(lessons|videos)\/[^/]+\/title$/.test(path));
        assert.deepEqual(Object.keys(body), ['title']);
      }
      assert.equal(changes.length, 3);
      assert.equal(await page.getByText(/youtube|يوتيوب|urlOrEmbedCode|Bunny/i).count(), 0);
      assert.equal(await page.getByRole('textbox').count(), 0);
      assert.equal(await page.getByRole('button', { name: /حذف الفيديو|إخفاء الفيديو|إضافة فيديو/ }).count(), 0);
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await mkdir('../artifacts/teacher-content-titles-20261124', { recursive: true });
      await page.screenshot({ path: `../artifacts/teacher-content-titles-20261124/${width}.png`, fullPage: true });
    } finally { await browser.close(); }
  });
}

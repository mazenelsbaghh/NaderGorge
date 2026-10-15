import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { chromium } from '@playwright/test';

for (const width of [390, 1440]) {
  test(`teacher creates an exam for the selected part without replacing existing exams (${width}px)`, { timeout: 120000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width, height: 900 } });
      page.setDefaultTimeout(30000);
      const user = { id: 'teacher', fullName: 'مدرس التجربة', roles: ['Teacher'], permissions: ['content.manage', 'exams.manage'],
        allowedDomains: ['teacher'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => {
        localStorage.setItem('accessToken', 'test-token');
        localStorage.setItem('user', JSON.stringify(user));
      }, user);
      const lesson = { lessonId: 'lesson', title: 'حصة التاريخ', summary: '', examId: 'lesson-exam', price: 0, order: 1,
        archiveMode: 'Active', resources: [], homework: [], commentsSummary: { pending: 0 }, videos: [
          { id: 'first-part', title: 'البارت الأول', examId: 'legacy-exam', exams: null },
          { id: 'second-part', title: 'البارت الثاني', exams: [{ examId: 'existing-exam', title: 'امتحان سابق' }] },
        ] };
      const submissions = [];
      let rejectSubmission = true;
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
        let data = [];
        if (path === '/auth/session') data = { user, authorizationVersion: 1 };
        if (path === '/teacher/context') data = { isOwner: true, permissionKeys: ['content'] };
        if (path === '/admin/lessons/lesson/cockpit') data = lesson;
        if (path === '/admin/exams/inline') {
          const submission = route.request().postDataJSON();
          submissions.push(submission);
          if (rejectSubmission) return route.fulfill({ status: 403, json: { success: false, message: 'ليس لديك صلاحية إنشاء هذا الامتحان' } });
          lesson.videos[1].exams.push({ examId: 'new-exam', title: submission.title });
          data = { id: 'new-exam' };
        }
        await route.fulfill({ json: { success: true, data } });
      });
      await page.goto('http://teacher.lvh.me:8738/teacher/packages/lessons/lesson');
      await page.getByRole('tab', { name: 'امتحانات البارتات', exact: true }).click();
      const firstPart = page.getByRole('region', { name: 'امتحانات البارت الأول', exact: true });
      const secondPart = page.getByRole('region', { name: 'امتحانات البارت الثاني', exact: true });
      assert.equal(await firstPart.getByRole('link').getAttribute('href'), '/teacher/packages/exams/legacy-exam');
      await secondPart.getByRole('button', { name: 'إنشاء امتحان للبارت', exact: true }).click();
      const title = secondPart.getByPlaceholder('مثال: الاختبار الأول على الدرس');
      await title.fill('امتحان البارت الثاني الجديد');
      const submit = secondPart.getByRole('button', { name: 'إنشاء امتحان الفيديو', exact: true });
      await submit.click();
      await page.getByText('ليس لديك صلاحية إنشاء هذا الامتحان', { exact: true }).waitFor();
      assert.equal(await title.inputValue(), 'امتحان البارت الثاني الجديد');
      assert.equal(await secondPart.getByRole('link').count(), 1);
      rejectSubmission = false;
      await submit.click();
      const createdExam = secondPart.getByRole('link', { name: /امتحان البارت الثاني الجديد/ });
      await createdExam.waitFor();
      assert.equal(await createdExam.getAttribute('href'), '/teacher/packages/exams/new-exam');
      assert.equal(await secondPart.getByRole('link').count(), 2);
      assert.equal(await firstPart.getByRole('link').count(), 1);
      assert.equal(submissions.length, 2);
      for (const submission of submissions) {
        assert.deepEqual(submission.target, { type: 'Video', id: 'second-part' });
        assert.equal(submission.durationMinutes, undefined);
        assert.equal(submission.displayQuestionCount, undefined);
      }
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await mkdir('../artifacts/teacher-part-exams', { recursive: true });
      await page.screenshot({ path: `../artifacts/teacher-part-exams/${width}.png`, fullPage: true });
    } finally { await browser.close(); }
  });
}

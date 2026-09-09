import assert from 'node:assert/strict';
import test from 'node:test';
import { webkit } from '@playwright/test';
import { mkdir } from 'node:fs/promises';

for (const surface of ['admin', 'teacher']) {
  test(`${surface} previews without attempts and edits the selected existing question`, { timeout: 90000 }, async () => {
    const browser = await webkit.launch();
    try {
      const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
      page.setDefaultTimeout(20000);
      const user = { id: 'author', fullName: 'محرر الاختبار', roles: [surface === 'admin' ? 'Admin' : 'Teacher'], permissions: ['content.manage', 'exams.manage'], allowedDomains: [surface], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => { localStorage.setItem('accessToken', 'test-token'); localStorage.setItem('user', JSON.stringify(user)); }, user);
      const homework = { homeworkId: 'homework', lessonId: 'lesson', title: 'واجب المعاينة', totalScore: 40, passingScore: 1, questionCount: 2, isActive: false, isMandatory: true, isRandomized: false, archiveMode: 'None', submissions: [], questions: [
        { homeworkQuestionId: 'q1', text: 'السؤال الأول', type: 'MCQ', points: 1, possibleAnswers: ['الاختيار الصحيح', 'الاختيار الثاني'], correctAnswerKey: 'الاختيار الصحيح' },
        { homeworkQuestionId: 'q2', text: 'السؤال الثاني', type: 'Essay', points: 2, writtenCorrection: 'نموذج الإجابة' },
      ] };
      const writes = [];
      let failSave = true;
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
        let data = [];
        if (path === '/auth/session') data = { user, authorizationVersion: 1 };
        if (path.endsWith('/dashboard')) data = homework;
        if (route.request().method() !== 'GET') {
          writes.push({ path, body: route.request().postDataJSON() });
          if (path.endsWith('/lesson/homework') && failSave) return route.fulfill({ status: 503, json: { success: false, message: 'تعذر حفظ الاختبار مؤقتًا' } });
          data = { id: 'homework' };
        }
        await route.fulfill({ json: { success: true, data } });
      });
      const base = surface === 'admin' ? '/admin/content/homework' : '/teacher/packages/homework';
      await page.goto(`http://${surface}.lvh.me:8738${base}/homework`);
      assert.equal(await page.getByRole('link', { name: 'بروفايل الواجب', exact: true }).getAttribute('href'), `${base}/homework`);
      await page.getByRole('button', { name: 'معاينة الواجب', exact: true }).click();
      const dialog = page.getByRole('dialog');
      await dialog.getByText('الاختيار الثاني', { exact: true }).waitFor();
      assert.equal(await dialog.getByText('نموذج الإجابة', { exact: true }).count(), 0);
      await dialog.getByLabel('إظهار الإجابات النموذجية').check();
      await dialog.getByText('نموذج الإجابة', { exact: true }).waitFor();
      assert.ok(await dialog.evaluate(element => element.scrollWidth <= element.clientWidth + 1));
      await mkdir('../artifacts/homework-authoring', { recursive: true });
      await page.screenshot({ path: `../artifacts/homework-authoring/${surface}-preview.png` });
      assert.equal(writes.length, 0);
      await dialog.getByRole('button', { name: 'إغلاق المعاينة' }).click();
      await page.getByRole('button', { name: 'تعديل السؤال 2', exact: true }).click();
      const selectedEditor = page.locator('#homework-edit-1 .ql-editor');
      await selectedEditor.fill('السؤال الثاني بعد التعديل');
      await page.getByRole('button', { name: 'حفظ التغييرات بالكامل' }).click();
      await page.getByRole('alert').filter({ hasText: 'تعذر حفظ الاختبار مؤقتًا' }).waitFor();
      assert.ok((await selectedEditor.innerText()).includes('بعد التعديل'));
      failSave = false;
      await page.getByRole('button', { name: 'حفظ التغييرات بالكامل' }).click();
      await page.waitForURL(`**${base}/homework`);
      const saved = writes.at(-1).body;
      assert.equal(saved.questions.length, 2);
      assert.equal(saved.questions[0].text, 'السؤال الأول');
      assert.ok(saved.questions[1].text.includes('السؤال الثاني بعد التعديل'));
      assert.equal(saved.questions[1].writtenCorrection, 'نموذج الإجابة');
      assert.equal(saved.totalScore, 40);
      assert.ok(writes.every(write => write.path.endsWith('/lesson/homework')));
      homework.submissions = [{ submissionId: 'attempt', studentId: 'student', studentName: 'طالب اختبار', status: 'InProgress', startedAt: '2026-09-09T12:00:00Z', scoreAchieved: 0 }];
      await page.reload();
      await page.getByRole('button', { name: 'تعديل السؤال 2', exact: true }).waitFor();
      assert.equal(await page.getByRole('button', { name: 'تعديل السؤال 2', exact: true }).isDisabled(), true);
      await page.goto(`http://${surface}.lvh.me:8738${base}/homework/add-question?question=q2`);
      assert.equal(await page.getByRole('button', { name: 'حفظ التغييرات بالكامل' }).isDisabled(), true);
    } finally { await browser.close(); }
  });
}

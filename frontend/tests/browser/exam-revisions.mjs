import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { webkit } from '@playwright/test';

for (const surface of ['admin', 'teacher']) {
  test(`${surface} exam revisions require a fresh preview and explicit confirmation`, { timeout: 90000 }, async () => {
    const browser = await webkit.launch();
    try {
      const page = await browser.newPage({ viewport: { width: 390, height: 844 }, hasTouch: true });
      page.setDefaultTimeout(20000);
      const user = { id: 'exam-author', fullName: 'محرر الامتحان', roles: [surface === 'admin' ? 'Admin' : 'Teacher'],
        permissions: ['content.manage', 'exams.manage'], allowedDomains: [surface], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => {
        localStorage.setItem('accessToken', 'test-token'); localStorage.setItem('user', JSON.stringify(user));
      }, user);
      const definition = { schemaVersion: 1, kind: 'exam', assessmentId: 'exam', title: 'امتحان المراجعة', description: '',
        totalScore: 20, passingScore: 10, durationMinutes: 30, isActive: true, isMandatory: true, isRandomized: false,
        displayQuestionCount: null, questions: [{ id: 'question', bankQuestionId: 'bank', order: 1, type: 0,
          text: 'اختر الإجابة', points: 4, audioUrl: null, imageUrl: null, writtenCorrection: null, hintText: null,
          baseText: null, mistakeStartIndex: null, mistakeEndIndex: null, correctAnswerKey: null,
          options: [{ id: 'option-a', text: 'أ', isCorrect: true }, { id: 'option-b', text: 'ب', isCorrect: false }] }] };
      const saves = [];
      let previewCount = 0;
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
        let data = [];
        if (path === '/auth/session') data = { user, authorizationVersion: 1 };
        if (path.endsWith('/editor')) data = { definition, attemptCount: 1, revisionToken: 'initial' };
        if (path.endsWith('/revision-preview')) {
          previewCount++;
          data = { revisionToken: `preview-${previewCount}`, attemptCount: 1, addedQuestions: 0, removedQuestions: 0,
            policy: route.request().postDataJSON().policy,
            attempts: [{ attemptId: 'prior-attempt', previousScore: 15, revisedScore: 30, requiresReview: false, requiresCompletion: false }] };
        }
        if (path.endsWith('/definition')) {
          saves.push(route.request().postDataJSON());
          return route.fulfill({ status: 409, json: { success: false, message: 'تغيّرت المحاولات. أعد المعاينة قبل الحفظ.' } });
        }
        await route.fulfill({ json: { success: true, data } });
      });
      const base = surface === 'admin' ? '/admin/content' : '/teacher/packages';
      await page.goto(`http://${surface}.lvh.me:8738${base}/exams/exam/add-question`);
      await page.getByLabel('الدرجة النهائية', { exact: true }).fill('40');
      await page.getByLabel('درجة النجاح', { exact: true }).fill('20');
      await page.getByRole('combobox', { name: /تطبيق التعديل/ }).selectOption('Regrade');
      await page.getByRole('combobox', { name: /الأسئلة المضافة/ }).selectOption('RequestCompletion');
      await page.getByRole('combobox', { name: /التصحيح اليدوي السابق/ }).selectOption('ReturnForReview');
      await page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true }).click();
      const save = page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' });
      assert.equal(await save.isDisabled(), true);
      const confirmation = page.getByRole('checkbox', { name: 'راجعت الأثر وأوافق على تطبيق اختياراتي على المحاولات السابقة.' });
      await confirmation.check();
      await page.getByLabel('اسم الامتحان', { exact: true }).fill('امتحان بعد التعديل');
      assert.equal(await save.count(), 0);
      await page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true }).click();
      assert.equal(await confirmation.isChecked(), false);
      await confirmation.check();
      await save.click();
      await page.getByRole('alert').filter({ hasText: 'تغيّرت المحاولات' }).waitFor();
      assert.equal(await page.getByLabel('اسم الامتحان', { exact: true }).inputValue(), 'امتحان بعد التعديل');
      assert.equal(saves.length, 1);
      assert.equal(saves[0].revisionToken, 'preview-2');
      assert.equal(saves[0].definition.totalScore, 40);
      assert.equal(saves[0].definition.passingScore, 20);
      assert.deepEqual(saves[0].definition.questions[0].options, definition.questions[0].options);
      assert.equal(saves[0].policy.scoreDecrease, 'Prevent');
      assert.equal(saves[0].policy.addedQuestions, 'RequestCompletion');
      assert.equal(saves[0].policy.manualGrades, 'ReturnForReview');
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await mkdir('../artifacts/assessment-revisions', { recursive: true });
      await page.screenshot({ path: `../artifacts/assessment-revisions/${surface}-editor.png`, fullPage: true });
    } finally { await browser.close(); }
  });
}

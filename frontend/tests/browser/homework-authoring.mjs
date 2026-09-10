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
      const definition = { schemaVersion: 1, kind: 'homework', assessmentId: 'homework', title: homework.title,
        description: '', totalScore: 40, passingScore: 1, durationMinutes: 30, isActive: false,
        isMandatory: true, isRandomized: false, displayQuestionCount: null,
        questions: homework.questions.map((question, index) => ({ id: question.homeworkQuestionId, bankQuestionId: question.homeworkQuestionId,
          order: index + 1, type: index === 0 ? 0 : 1, text: question.text, points: question.points,
          audioUrl: null, imageUrl: null, writtenCorrection: question.writtenCorrection ?? null, hintText: null,
          baseText: null, mistakeStartIndex: null, mistakeEndIndex: null, correctAnswerKey: question.correctAnswerKey ?? null,
          options: (question.possibleAnswers ?? []).map((text, index) => ({ id: `option-${index}`, text, isCorrect: index === 0 })) })) };
      const template = { id: 'parent-result', name: 'نتيجة الواجب', language: 'ar', category: 'UTILITY', status: 'APPROVED',
        fingerprint: 'a'.repeat(64), components: [{ type: 'BODY', text: 'الدرجة {{1}} من {{2}}' }] };
      const writes = [];
      let failSave = true;
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
        let data = [];
        if (path === '/auth/session') data = { user, authorizationVersion: 1 };
        if (path.endsWith('/notification-templates')) data = [template];
        if (path.endsWith('/dashboard')) data = homework;
        if (path.endsWith('/editor')) data = { definition, attemptCount: homework.submissions.length, revisionToken: 'original' };
        if (route.request().method() !== 'GET') {
          writes.push({ path, body: route.request().postDataJSON() });
          if (path.endsWith('/definition') && failSave) return route.fulfill({ status: 503, json: { success: false, message: 'تعذر حفظ الاختبار مؤقتًا' } });
          data = path.endsWith('/revision-preview')
            ? { revisionToken: 'reviewed', attemptCount: homework.submissions.length, addedQuestions: 0, removedQuestions: 0, attempts: [], policy: route.request().postDataJSON().policy }
            : { definition, attemptCount: homework.submissions.length, revisionToken: 'saved' };
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
      const selectedEditor = page.locator('#assessment-question-editor .ql-editor').first();
      await selectedEditor.fill('السؤال الثاني بعد التعديل');
      await page.getByRole('checkbox', { name: 'إرسال واتساب تلقائيًا بعد اكتمال التصحيح' }).check();
      await page.getByRole('combobox', { name: 'قالب رسالة النتيجة' }).selectOption(template.id);
      await page.getByRole('combobox', { name: 'المتغير 1 (نص الرسالة)' }).selectOption('Score');
      await page.getByRole('combobox', { name: 'المتغير 2 (نص الرسالة)' }).selectOption('TotalScore');
      await page.getByText('الدرجة 35 من 40', { exact: true }).waitFor();
      await page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true }).click();
      await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).click();
      await page.getByRole('alert').filter({ hasText: 'تعذر حفظ الاختبار مؤقتًا' }).waitFor();
      assert.ok((await selectedEditor.innerText()).includes('بعد التعديل'));
      failSave = false;
      await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).click();
      await page.waitForURL(`**${base}/homework`);
      const saved = writes.at(-1).body.definition;
      assert.equal(saved.questions.length, 2);
      assert.equal(saved.questions[0].text, 'السؤال الأول');
      assert.ok(saved.questions[1].text.includes('السؤال الثاني بعد التعديل'));
      assert.equal(saved.questions[1].writtenCorrection, 'نموذج الإجابة');
      assert.equal(saved.totalScore, 40);
      assert.deepEqual(saved.parentNotification, { enabled: true, templateId: template.id,
        templateFingerprint: template.fingerprint, parameters: [{ source: 'Score' }, { source: 'TotalScore' }] });
      assert.ok(writes.every(write => /\/(revision-preview|definition)$/.test(write.path)));
      const saves = writes.filter(write => write.path.endsWith('/definition'));
      assert.equal(saves[0].body.operationId, saves[1].body.operationId);
      assert.equal(saves[1].body.policy.previousAttempts, 'Preserve');
      homework.submissions = [{ submissionId: 'attempt', studentId: 'student', studentName: 'طالب اختبار', status: 'InProgress', startedAt: '2026-09-09T12:00:00Z', scoreAchieved: 0 }];
      await page.reload();
      await page.getByRole('button', { name: 'تعديل السؤال 2', exact: true }).waitFor();
      assert.equal(await page.getByRole('button', { name: 'تعديل السؤال 2', exact: true }).isDisabled(), false);
      await page.goto(`http://${surface}.lvh.me:8738${base}/homework/add-question?question=q2`);
      await page.getByRole('combobox', { name: /تطبيق التعديل/ }).selectOption('Regrade');
      await page.getByRole('combobox', { name: /انخفاض درجات الطلاب/ }).selectOption('Allow');
      await page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true }).click();
      assert.equal(await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).isDisabled(), true);
      await page.getByRole('checkbox', { name: 'راجعت الأثر وأوافق على تطبيق اختياراتي على المحاولات السابقة.' }).check();
      await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).click();
      await page.waitForURL(`**${base}/homework`);
      assert.equal(writes.at(-1).body.policy.scoreDecrease, 'Allow');
      assert.equal(writes.at(-1).body.confirmPreviousAttempts, true);
    } finally { await browser.close(); }
  });
}

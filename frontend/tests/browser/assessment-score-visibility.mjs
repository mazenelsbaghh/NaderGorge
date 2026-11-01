import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium } from '@playwright/test';
import { mkdir } from 'node:fs/promises';

const port = process.env.ESSAY_BROWSER_PORT || '8738';
async function signIn(page, surface) {
  const user = { id: 'visibility-test', fullName: 'اختبار النتيجة', roles: [surface === 'admin' ? 'Admin' : 'Student'],
    permissions: ['content.manage'], allowedDomains: [surface], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
  await page.addInitScript(user => {
    localStorage.setItem('accessToken', 'synthetic-test-token'); localStorage.setItem('user', JSON.stringify(user));
  }, user);
  return user;
}

for (const width of [390, 1280]) test(`homework settings save grading, one attempt and independent visibility at ${width}px`, { timeout: 60000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width, height: 844 } });
    const user = await signIn(page, 'admin');
    const definition = { schemaVersion: 1, kind: 'homework', assessmentId: 'work', title: 'واجب المقالي',
      totalScore: 10, passingScore: 5, isActive: true, isMandatory: true, isRandomized: false,
      homeworkGradingVersion: 1, essayGradingMode: 'AutomaticFullCredit', questions: [{ id: 'essay', type: 1,
        order: 1, text: 'فسر الجاذبية', points: 10, options: [] }] };
    let saved;
    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname;
      let data = [];
      if (path.endsWith('/auth/session')) data = { user, authorizationVersion: 1 };
      if (path.endsWith('/editor')) data = { definition, attemptCount: 1 };
      if (path.endsWith('/revision-preview')) data = { revisionToken: 'preview', attempts: [], addedQuestions: 0,
        removedQuestions: 0, attemptCount: 1, policy: route.request().postDataJSON().policy };
      if (path.endsWith('/definition')) {
        saved = route.request().postDataJSON();
        return route.fulfill({ status: 409, json: { success: false, message: 'مراجعة الاختبار' } });
      }
      return route.fulfill({ json: { success: true, data } });
    });
    await page.goto(`http://admin.lvh.me:${port}/admin/content/homework/work/add-question`);
    const mode = page.getByRole('combobox', { name: /تصحيح الأسئلة المقالية/ });
    await mode.waitFor();
    assert.equal(await page.getByRole('radio').count(), 4);
    await page.getByRole('radio', { name: /^درجة كاملة من ٧٠٪/ }).check();
    await page.getByRole('switch', { name: /^محاولة واحدة فقط/ }).check();
    await page.getByRole('switch', { name: /^إظهار النتيجة إظهار إجمالي/ }).uncheck();
    assert.equal(await page.getByRole('switch', { name: /^إظهار الإجابات والتصحيح/ }).isChecked(), true);
    await mode.selectOption(width === 390 ? 'Manual' : 'ModelAnswerOnly');
    await page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true }).click();
    await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).click();
    await page.getByRole('alert').filter({ hasText: 'مراجعة الاختبار' }).waitFor();
    assert.equal(saved.definition.essayGradingMode, width === 390 ? 'Manual' : 'ModelAnswerOnly');
    assert.equal(saved.definition.singleAttemptOnly, true);
    assert.equal(saved.definition.showScoreToStudents, false);
    assert.notEqual(saved.definition.showAnswersToStudents, false);
    assert.equal(saved.policy.previousAttempts, 'Preserve');
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    await mkdir('../artifacts/private/assessment-parity-20261101/screenshots', { recursive: true });
    await page.getByRole('group', { name: 'إعدادات الواجب', exact: true }).screenshot({ path: `../artifacts/private/assessment-parity-20261101/screenshots/settings-${width}.png` });
  } finally { await browser.close(); }
});

for (const kind of ['exam', 'homework']) for (const pending of [false, true]) {
  test(`${kind} retains correction, feedback and question marks with aggregate hidden, pending=${pending}`, { timeout: 60000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
      const user = await signIn(page, 'student');
      const shared = { scoreVisible: false, answersVisible: true, allowRetake: false, totalScore: null, isPassed: true,
        evaluation: 'ممتاز', partialResult: pending ? { scoreAchieved: null, totalScore: null, pendingQuestionCount: 1 } : null };
      const question = { order: 1, gradingFeedback: 'إجابة ممتازة لأن التعليل يوضح العلاقة بين الكتل.', isCorrect: true };
      const homework = { ...shared, homeworkId: 'work', submissionId: 'submission', title: 'واجب', score: null,
        status: pending ? 'PendingReview' : 'Graded', totalQuestions: 1, correctAnswers: 1, wrongAnswers: 0, ungradedAnswers: pending ? 1 : 0,
        questionReviews: [{ ...question, questionId: 'essay', questionType: 1, text: 'فسر الجاذبية', providedAnswer: 'الكتل تتجاذب',
          correctAnswer: 'الجاذبية بين الكتل', maxPoints: 4, scoreReceived: 4 }] };
      const exam = { ...shared, examId: 'exam', attemptId: 'attempt', scoreAchieved: null, blocksNextLesson: false,
        isTimeExpired: false, resultState: pending ? 'PartiallyGraded' : 'Completed',
        questions: [{ ...question, examQuestionId: 'essay', questionText: 'فسر الجاذبية', selectedOptionText: 'الكتل تتجاذب',
          isAnswered: true, pointsAwarded: 4, maximumPoints: 4, correctOptionText: 'الجاذبية بين الكتل' }] };
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname;
        let data = {};
        if (path.endsWith('/auth/session')) data = { user, authorizationVersion: 1 };
        if (path.endsWith('/work/start')) data = { homeworkId: 'work', submissionId: 'submission', alreadyCompleted: true, questions: [] };
        if (path.endsWith('/work/result')) data = homework;
        if (path.endsWith('/latest-result')) data = exam;
        if (path.endsWith('/grading-status')) data = { attemptId: 'attempt', resultState: exam.resultState, essays: [] };
        return route.fulfill({ json: { success: true, data } });
      });
      await page.goto(`http://app.lvh.me:${port}/student/${kind === 'homework' ? 'homework/work' : 'exams/exam'}`);
      if (kind === 'homework') await page.getByRole('button', { name: 'نعم، دخول الواجب', exact: true }).click();
      await page.getByText(question.gradingFeedback, { exact: true }).waitFor();
      assert.ok(await page.getByText('الجاذبية بين الكتل', { exact: true }).isVisible());
      assert.ok(await page.getByText(kind === 'homework' ? '4 / 4 نقطة' : '4 من 4 نقطة', { exact: true }).isVisible());
      assert.equal(await page.getByText('الدرجة', { exact: true }).count(), 0);
      assert.equal(await page.getByText(/^\d+%$/).count(), 0);
      assert.equal(await page.getByRole('button', { name: /إعادة حل/ }).count(), 0);
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await mkdir('../artifacts/private/assessment-parity-20261101/screenshots', { recursive: true });
      await page.screenshot({ path: `../artifacts/private/assessment-parity-20261101/screenshots/${kind}-${pending ? 'pending' : 'graded'}.png`, fullPage: true });
    } finally { await browser.close(); }
  });
}

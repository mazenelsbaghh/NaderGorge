import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium } from '@playwright/test';
import { mkdir } from 'node:fs/promises';

const port = process.env.ESSAY_BROWSER_PORT || '8738';
async function signIn(page, surface) {
  const user = { id: 'homework-test', fullName: 'اختبار الواجب', roles: [surface === 'admin' ? 'Admin' : 'Student'],
    permissions: ['content.manage'], allowedDomains: [surface], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
  await page.addInitScript(user => {
    localStorage.setItem('accessToken', 'synthetic-test-token');
    localStorage.setItem('user', JSON.stringify(user));
  }, user);
  return user;
}

for (const width of [390, 1280]) {
  test(`homework editor saves the pending visibility switch at ${width}px (synthetic HTTP)`, { timeout: 60000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width, height: 844 } });
      const user = await signIn(page, 'admin');
      const definition = { schemaVersion: 1, kind: 'homework', assessmentId: 'work', title: 'واجب المقالي', description: '',
        totalScore: 10, passingScore: 5, durationMinutes: null, isActive: true, isMandatory: true,
        isRandomized: false, displayQuestionCount: null, showResultsWhileEssayPending: false,
        questions: [{ id: 'essay', order: 1, type: 1, text: 'فسر الجاذبية', points: 10,
          writtenCorrection: 'قوة تجذب الكتل.', options: [] }] };
      const saves = [];
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname;
        let response = [];
        if (path.endsWith('/auth/session')) response = { user, authorizationVersion: 1 };
        if (path.endsWith('/editor')) response = { definition, attemptCount: 1 };
        if (path.endsWith('/revision-preview')) response = { revisionToken: 'preview', attemptCount: 1,
          addedQuestions: 0, removedQuestions: 0, policy: route.request().postDataJSON().policy, attempts: [] };
        if (path.endsWith('/definition')) {
          saves.push(route.request().postDataJSON());
          return route.fulfill({ status: 409, json: { success: false, message: 'المعاينة التجريبية تحتاج تحديث.' } });
        }
        await route.fulfill({ json: { success: true, data: response } });
      });
      await page.goto(`http://admin.lvh.me:${port}/admin/content/homework/work/add-question`);
      const toggle = page.getByRole('switch', { name: /إظهار النتيجة أثناء تصحيح المقالي/ });
      assert.equal(await toggle.isChecked(), false);
      await page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true }).click();
      await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).waitFor();
      await toggle.check();
      assert.equal(await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).count(), 0);
      await page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true }).click();
      await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).click();
      await page.getByRole('alert').filter({ hasText: 'المعاينة التجريبية' }).waitFor();
      assert.equal(saves.length, 1);
      assert.equal(saves[0].definition.showResultsWhileEssayPending, true);
      assert.equal(saves[0].policy.previousAttempts, 'Preserve');
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await mkdir('../artifacts/homework-pending-results', { recursive: true });
      await page.locator('fieldset').filter({ has: toggle }).screenshot({ path: `../artifacts/homework-pending-results/editor-${width}.png` });
    } finally { await browser.close(); }
  });
}

for (const scenario of [{ enabled: true, essayOnly: false }, { enabled: false, essayOnly: false }, { enabled: true, essayOnly: true }]) {
  test(`homework pending visibility ${scenario.enabled}, essay only ${scenario.essayOnly} (synthetic HTTP)`, { timeout: 60000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
      const user = await signIn(page, 'student');
      let completed = false;
      const essay = { questionId: 'essay', order: 2, questionType: 1, text: 'فسر الجاذبية', providedAnswer: 'إجابة الطالب',
        correctAnswer: 'قوة تجذب الكتل.', maxPoints: 2, scoreReceived: null, isCorrect: null };
      const reviews = scenario.essayOnly ? [essay] : [{ questionId: 'choice', order: 1, questionType: 0, text: 'اختر الصحيح',
        providedAnswer: 'أ', correctAnswer: 'أ', maxPoints: 8, scoreReceived: 8, isCorrect: true }, essay];
      const pending = { homeworkId: 'work', submissionId: 'submission', title: 'واجب المقالي', score: scenario.essayOnly ? 0 : 8,
        totalScore: scenario.essayOnly ? 2 : 10, passingScore: 5, isPassed: false, status: 'PendingReview', totalQuestions: reviews.length,
        correctAnswers: scenario.essayOnly ? 0 : 1, wrongAnswers: 0, ungradedAnswers: 1,
        questionReviews: scenario.enabled ? reviews : [],
        partialResult: scenario.enabled ? { scoreAchieved: scenario.essayOnly ? 0 : 8, totalScore: scenario.essayOnly ? 0 : 8, pendingQuestionCount: 1 } : null };
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname;
        let response = {};
        if (path.endsWith('/auth/session')) response = { user, authorizationVersion: 1 };
        if (path.endsWith('/work/start')) response = { homeworkId: 'work', submissionId: 'submission', alreadyCompleted: true, questions: [] };
        if (path.endsWith('/work/result')) response = completed ? { ...pending, status: 'Graded', score: 9, totalScore: 10,
          isPassed: true, partialResult: null, ungradedAnswers: 0, questionReviews: reviews.map(q => q.questionId === 'essay'
            ? { ...q, scoreReceived: 1, isCorrect: true } : q) } : pending;
        await route.fulfill({ json: { success: true, data: response } });
      });
      await page.goto(`http://app.lvh.me:${port}/student/homework/work?packageId=package&lessonId=lesson`);
      await page.getByRole('button', { name: 'نعم، دخول الواجب', exact: true }).click();
      if (!scenario.enabled) {
        await page.getByRole('heading', { name: 'الواجب قيد التصحيح', exact: true }).waitFor();
        assert.equal(await page.getByText('قوة تجذب الكتل.', { exact: true }).count(), 0);
        assert.equal(await page.getByRole('heading', { name: 'مراجعة الورقة كاملة' }).count(), 0);
        return;
      }
      await page.getByRole('heading', { name: 'درجتك في الأسئلة المصححة' }).waitFor();
      await page.getByText(scenario.essayOnly ? 'لا توجد درجات مصححة بعد' : '8 من 8', { exact: true }).waitFor();
      assert.equal(await page.getByRole('link', { name: 'العودة للحصة', exact: true }).getAttribute('href'), '/student/packages/package/lessons/lesson');
      const essayReview = page.locator('article').filter({ has: page.getByText('فسر الجاذبية', { exact: true }) });
      assert.ok(await essayReview.getByText('قيد التصحيح', { exact: true }).isVisible());
      assert.ok(await essayReview.getByText('قوة تجذب الكتل.', { exact: true }).isVisible());
      assert.equal(await essayReview.getByText('خاطئة ✗', { exact: true }).count(), 0);
      assert.equal(await essayReview.getByText('0 / 2 نقطة', { exact: true }).count(), 0);
      assert.equal(await page.getByRole('button', { name: 'إعادة حل الواجب' }).count(), 0);
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await mkdir('../artifacts/homework-pending-results', { recursive: true });
      await page.getByRole('region', { name: 'النتيجة الجزئية' }).screenshot({ path: `../artifacts/homework-pending-results/score-${scenario.essayOnly ? 'essay' : 'mixed'}.png` });
      await essayReview.screenshot({ path: `../artifacts/homework-pending-results/review-${scenario.essayOnly ? 'essay' : 'mixed'}.png` });
      if (!scenario.essayOnly) {
        completed = true;
        await page.getByText('9 / 10', { exact: true }).waitFor();
        assert.equal(await page.getByRole('heading', { name: 'درجتك في الأسئلة المصححة' }).count(), 0);
        await essayReview.getByText('1 / 2 نقطة', { exact: true }).waitFor();
        assert.equal(await essayReview.getByText('قيد التصحيح', { exact: true }).count(), 0);
      }
    } finally { await browser.close(); }
  });
}

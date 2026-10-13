import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium } from '@playwright/test';
import { mkdir } from 'node:fs/promises';

const port = process.env.ESSAY_BROWSER_PORT || '8738';
async function signedInPage(browser, surface, width) {
  const page = await browser.newPage({ viewport: { width, height: 844 } });
  const user = { id: 'essay-mode-test', fullName: 'اختبار المقالي', roles: [surface === 'admin' ? 'Admin' : 'Student'],
    permissions: ['content.manage', 'exams.manage'], allowedDomains: [surface], allowedNavbarItems: [],
    profileComplete: true, authorizationVersion: 1 };
  await page.addInitScript(user => {
    localStorage.setItem('accessToken', 'synthetic-test-token');
    localStorage.setItem('user', JSON.stringify(user));
  }, user);
  return { page, user };
}

for (const width of [390, 1280]) {
  test(`exam editor saves grading mode and invalidates stale preview at ${width}px (synthetic HTTP)`, { timeout: 90000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const { page, user } = await signedInPage(browser, 'admin', width);
      const definition = { schemaVersion: 1, kind: 'exam', assessmentId: 'exam', title: 'امتحان المقالي', description: '',
        totalScore: 10, passingScore: 5, durationMinutes: null, isActive: true, isMandatory: true,
        isRandomized: false, displayQuestionCount: null, essayGradingMode: 'Automatic', questions: [{
          id: 'essay', bankQuestionId: 'bank', order: 1, type: 1, text: 'فسر الجاذبية', points: 10,
          writtenCorrection: 'قوة تجذب الكتل.', options: [] }] };
      const saves = [];
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname;
        let data = [];
        if (path.endsWith('/auth/session')) data = { user, authorizationVersion: 1 };
        if (path.endsWith('/editor')) data = { definition, attemptCount: 1 };
        if (path.endsWith('/revision-preview')) data = { revisionToken: 'preview', attemptCount: 1,
          addedQuestions: 0, removedQuestions: 0, policy: route.request().postDataJSON().policy, attempts: [] };
        if (path.endsWith('/definition')) {
          saves.push(route.request().postDataJSON());
          return route.fulfill({ status: 409, json: { success: false, message: 'المعاينة التجريبية تحتاج تحديث.' } });
        }
        return route.fulfill({ json: { success: true, data } });
      });
      await page.goto(`http://admin.lvh.me:${port}/admin/content/exams/exam/add-question`);
      const mode = page.getByRole('combobox', { name: /تصحيح الأسئلة المقالية/ });
      await mode.selectOption('Manual');
      await page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true }).click();
      await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).waitFor();
      await mode.selectOption('ModelAnswerOnly');
      assert.equal(await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).count(), 0);
      await page.getByRole('button', { name: 'معاينة تأثير التعديل', exact: true }).click();
      await page.getByRole('button', { name: 'حفظ التعديلات المؤكدة' }).click();
      await page.getByRole('alert').filter({ hasText: 'المعاينة التجريبية' }).waitFor();
      assert.equal(saves.length, 1);
      assert.equal(saves[0].definition.essayGradingMode, 'ModelAnswerOnly');
      assert.equal(saves[0].policy.previousAttempts, 'Preserve');
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await mkdir('../artifacts/exam-essay-grading', { recursive: true });
      await page.locator('fieldset').filter({ has: mode }).screenshot({ path: `../artifacts/exam-essay-grading/editor-${width}.png` });
    } finally { await browser.close(); }
  });
}

test('student sees excluded essay and model answer without wrong mark (synthetic HTTP)', { timeout: 90000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const { page, user } = await signedInPage(browser, 'student', 390);
    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname;
      let data = {};
      if (path.endsWith('/auth/session')) data = { user, authorizationVersion: 1 };
      if (path.endsWith('/latest-result')) data = {
        attemptId: 'attempt', scoreAchieved: 2, totalScore: 2, isPassed: true, blocksNextLesson: false,
        evaluation: 'ممتاز', isTimeExpired: false, resultState: 'Completed', questions: [{
          examQuestionId: 'essay', order: 1, questionText: 'فسر الجاذبية', selectedOptionText: 'إجابة الطالب',
          isAnswered: true, isCorrect: false, pointsAwarded: 0, maximumPoints: 0, isExcludedFromScore: true,
          correctOptionText: 'قوة تجذب الكتل.' }],
      };
      return route.fulfill({ json: { success: true, data } });
    });
    await page.goto(`http://app.lvh.me:${port}/student/exams/exam`);
    await page.getByText('بدون تصحيح', { exact: true }).waitFor();
    assert.equal(await page.getByText('خاطئة ✗', { exact: true }).count(), 0);
    assert.equal(await page.getByText('0 من 0 نقطة', { exact: true }).count(), 0);
    assert.ok(await page.getByText('قوة تجذب الكتل.', { exact: true }).isVisible());
    assert.ok(await page.getByText('المقالي مستبعد من حساب الدرجة النهائية', { exact: true }).isVisible());
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    await page.evaluate(async () => {
      await Promise.all(document.getAnimations().filter(animation => animation.effect?.getTiming().iterations !== Infinity)
        .map(animation => animation.finished.catch(() => undefined)));
    });
    await page.getByRole('heading', { name: 'مراجعة الورقة كاملة' }).locator('..')
      .screenshot({ path: '../artifacts/exam-essay-grading/student-model-answer.png' });
  } finally { await browser.close(); }
});

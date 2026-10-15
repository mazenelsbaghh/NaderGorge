import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium } from '@playwright/test';
import { mkdir } from 'node:fs/promises';

const base = process.env.ESSAY_BROWSER_BASE_URL || 'http://app.lvh.me:8738';
for (const scenario of [{ enabled: true, essayOnly: false }, { enabled: false, essayOnly: false }, { enabled: true, essayOnly: true }]) {
  test(`pending result switch ${scenario.enabled}, essay only ${scenario.essayOnly} (synthetic HTTP)`, { timeout: 60000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
      const user = { id: 'pending-test-student', fullName: 'طالب اختبار', roles: ['Student'], permissions: [],
        allowedDomains: ['student'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => {
        localStorage.setItem('accessToken', 'synthetic-test-token');
        localStorage.setItem('user', JSON.stringify(user));
      }, user);
      let completed = false;
      const essay = { examQuestionId: 'essay', order: 3, questionText: 'فسر الجاذبية', selectedOptionText: 'إجابة الطالب',
        isAnswered: true, isCorrect: false, pointsAwarded: 0, maximumPoints: 2, isPendingGrading: true,
        correctOptionText: scenario.enabled ? 'قوة تجذب الكتل.' : null };
      const questions = scenario.essayOnly ? [essay] : [
        { examQuestionId: 'right', order: 1, questionText: 'اختر المجموعة الصحيحة', isAnswered: true, isCorrect: true,
          pointsAwarded: 6, maximumPoints: 6, selectedOptionText: 'أ', correctOptionText: 'أ' },
        { examQuestionId: 'wrong', order: 2, questionText: 'اختر التفسير الصحيح', isAnswered: true, isCorrect: false,
          pointsAwarded: 0, maximumPoints: 2, selectedOptionText: 'ب', correctOptionText: 'أ' }, essay];
      const pending = { attemptId: 'attempt', scoreAchieved: scenario.essayOnly ? 0 : 6, totalScore: scenario.essayOnly ? 2 : 10,
        isPassed: false, blocksNextLesson: false, evaluation: 'قيد التصحيح', isTimeExpired: false,
        resultState: 'PartiallyGraded', lessonId: 'lesson', packageId: 'package', questions,
        partialResult: scenario.enabled ? { scoreAchieved: scenario.essayOnly ? 0 : 6, totalScore: scenario.essayOnly ? 0 : 8, pendingQuestionCount: 1 } : null };
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname;
        let data = {};
        if (path.endsWith('/auth/session')) data = { user, authorizationVersion: 1 };
        if (path.endsWith('/latest-result')) data = pending;
        if (path.endsWith('/grading-status')) data = { attemptId: 'attempt', resultState: completed ? 'Completed' : 'PartiallyGraded',
          essays: [{ essaySubmissionId: 'submission', questionId: 'essay-bank', status: completed ? 'TeacherGraded' : 'WaitTeacher' }] };
        if (path.endsWith('/attempts/attempt/result')) data = { ...pending, scoreAchieved: 7, totalScore: 10,
          isPassed: true, evaluation: 'جيد', resultState: 'Completed', partialResult: null,
          questions: questions.map(q => q.examQuestionId === 'essay' ? { ...q, isPendingGrading: false, pointsAwarded: 1,
            gradingFeedback: 'تم اعتماد الدرجة.', correctOptionText: 'قوة تجذب الكتل.' } : q) };
        await route.fulfill({ json: { success: true, data } });
      });
      await page.goto(`${base}/student/exams/pending-exam`);
      if (!scenario.enabled) {
        await page.getByText('إجاباتك المقالية محفوظة للمراجعة', { exact: true }).waitFor();
        assert.equal(await page.getByRole('heading', { name: 'مراجعة الورقة كاملة' }).count(), 0);
        assert.equal(await page.getByText('قوة تجذب الكتل.', { exact: true }).count(), 0);
        return;
      }
      await page.getByRole('heading', { name: 'درجتك في الأسئلة المصححة' }).waitFor();
      assert.equal(await page.getByRole('heading', { name: 'حاول مرة أخرى', exact: true }).count(), 0);
      assert.equal(await page.getByRole('button', { name: 'إعادة الامتحان', exact: true }).count(), 0);
      assert.equal(await page.getByRole('link', { name: 'العودة للحصة', exact: true }).getAttribute('href'), '/student/packages/package/lessons/lesson');
      const essayReview = page.locator('article').filter({ has: page.getByText('فسر الجاذبية', { exact: true }) });
      assert.ok(await essayReview.getByText('قيد التصحيح', { exact: true }).isVisible());
      assert.equal(await essayReview.getByText('خاطئة ✗', { exact: true }).count(), 0);
      assert.equal(await essayReview.getByText('0 من 2 نقطة', { exact: true }).count(), 0);
      assert.ok(await essayReview.getByText('قوة تجذب الكتل.', { exact: true }).isVisible());
      if (scenario.essayOnly) await page.getByText('لا توجد درجات مصححة بعد', { exact: true }).waitFor();
      else await page.getByText('6 من 8', { exact: true }).waitFor();
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await mkdir('../artifacts/exam-pending-results', { recursive: true });
      await page.getByRole('region', { name: 'النتيجة الجزئية' })
        .screenshot({ path: `../artifacts/exam-pending-results/score-${scenario.essayOnly ? 'essay' : 'mixed'}.png` });
      await essayReview.screenshot({ path: `../artifacts/exam-pending-results/review-${scenario.essayOnly ? 'essay' : 'mixed'}.png` });
      if (!scenario.essayOnly) {
        completed = true;
        await page.getByText('7 / 10', { exact: true }).waitFor();
        assert.equal(await page.getByRole('heading', { name: 'درجتك في الأسئلة المصححة' }).count(), 0);
        assert.equal(await page.getByText('قيد التصحيح', { exact: true }).count(), 0);
        assert.ok(await page.getByText('تم اعتماد الدرجة.', { exact: true }).first().isVisible());
      }
    } finally { await browser.close(); }
  });
}

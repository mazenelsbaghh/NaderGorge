import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { webkit } from '@playwright/test';

for (const kind of ['homework', 'exams']) {
  test(`${kind} completion keeps old drafts separate and submits the current revision`, { timeout: 90000 }, async () => {
    const browser = await webkit.launch();
    try {
      const page = await browser.newPage({ viewport: { width: 390, height: 844 }, hasTouch: true });
      page.setDefaultTimeout(20000);
      const user = { id: 'completion-student', fullName: 'طالب اختبار', roles: ['Student'], permissions: [],
        allowedDomains: ['student'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(({ user, kind }) => {
        localStorage.setItem('accessToken', 'completion-test-token');
        localStorage.setItem('user', JSON.stringify(user));
        localStorage.setItem(`onboarding_ack_${user.id}`, '1');
        localStorage.setItem(`${kind === 'exams' ? 'exam' : 'homework'}_answers_attempt`, JSON.stringify({ oldQuestion: 'old answer' }));
      }, { user, kind });
      let sent;
      let rejectSubmission = true;
      await page.route('**/api/**', async route => {
        const url = new URL(route.request().url());
        const path = url.pathname.replace(/^\/api/, '');
        let data = [];
        if (path === '/auth/session') data = { user, authorizationVersion: 1 };
        if (path === '/student/shell-bootstrap') data = { unreadNotificationsCount: 0, currentBalance: 0, themePreferences: {}, hasSeenTrackingCodePopup: true };
        if (path.endsWith('/latest-result')) return route.fulfill({ status: 404, json: { success: false, message: 'Completion required' } });
        if (path.endsWith('/start')) data = {
          homeworkId: 'assessment', submissionId: 'attempt', attemptId: 'attempt', revisionId: 'revision-two',
          title: 'استكمال التقييم', description: '', totalScore: 20, passingScore: 10,
          startedAt: new Date().toISOString(), durationMinutes: 30, remainingSeconds: 1800, alreadyCompleted: false,
          questions: [{ id: 'newQuestion', text: 'السؤال المضاف', questionType: 0, type: 'MCQ', maxPoints: 4, points: 4,
            possibleAnswers: ['اختيار جديد', 'اختيار آخر'], options: [{ id: 'optionC', text: 'اختيار جديد' }, { id: 'optionD', text: 'اختيار آخر' }] }]
        };
        if (path.includes('/submit')) {
          sent = { revisionId: url.searchParams.get('revisionId'), answers: route.request().postDataJSON() };
          if (rejectSubmission) return route.fulfill({ status: 503, json: { success: false, message: 'تعذر الحفظ مؤقتًا' } });
          data = kind === 'homework' ? true : { attemptId: 'attempt', resultState: 'Completed', questions: [],
            scoreAchieved: 20, totalScore: 20, isPassed: true, evaluation: 'ممتاز', blocksNextLesson: false, isTimeExpired: false };
        }
        if (path === '/homework/assessment/result') data = { homeworkId: 'assessment', submissionId: 'attempt', title: 'استكمال التقييم',
          score: 20, totalScore: 20, isPassed: true, evaluation: 'ممتاز', status: 'Graded', totalQuestions: 2,
          correctAnswers: 2, wrongAnswers: 0, ungradedAnswers: 0, questionReviews: [] };
        await route.fulfill({ json: { success: true, data } });
      });
      await page.goto(`http://app.lvh.me:8738/student/${kind}/assessment`);
      await page.getByRole('status').filter({ hasText: 'استكمال الأسئلة المضافة فقط' }).waitFor();
      await page.getByText('اختيار جديد', { exact: true }).click();
      assert.equal(await page.getByRole('radio', { name: 'اختيار جديد', exact: true }).isChecked(), true);
      const submit = page.getByRole('button', { name: kind === 'homework' ? 'تسليم الواجب' : 'تسليم وإنهاء', exact: true });
      await submit.click();
      await page.getByRole('alert').filter({ hasText: 'تعذر الحفظ مؤقتًا' }).waitFor();
      assert.equal(sent.revisionId, 'revision-two');
      assert.deepEqual(sent.answers, kind === 'homework'
        ? [{ questionId: 'newQuestion', providedAnswer: 'اختيار جديد' }]
        : [{ examQuestionId: 'newQuestion', selectedOptionId: 'optionC' }]);
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await mkdir('../artifacts/assessment-revisions', { recursive: true });
      await page.screenshot({ path: `../artifacts/assessment-revisions/${kind}-completion.png`, fullPage: true });
      rejectSubmission = false;
      await submit.click();
      await page.getByText('ممتاز', { exact: true }).first().waitFor();
      assert.equal(await page.evaluate(kind => localStorage.getItem(`${kind === 'exams' ? 'exam' : 'homework'}_answers_attempt`), kind), JSON.stringify({ oldQuestion: 'old answer' }));
    } finally { await browser.close(); }
  });
}

import { expect, test, type Page, type Route, type WebSocketRoute } from '@playwright/test';
import type { ActiveExamAttemptDto, ExamResultDto } from '../../src/services/exam-service';
import type { StartHomeworkAttemptDto, HomeworkResultDto } from '../../src/services/homework-service';

const assessmentId = '97000000-0000-0000-0000-000000000001';
const reply = (route: Route, data: unknown, status = 200) => route.fulfill({ status, json: { success: status === 200, data } });

async function studentSession(page: Page) {
  const user = { id: '97000000-0000-0000-0000-000000000099', fullName: 'Retry test student', phone: '20000000001', roles: ['Student'], permissions: [], profileComplete: true, allowedDomains: ['student'], allowedNavbarItems: [], authorizationVersion: 1 };
  await page.addInitScript(authUser => {
    localStorage.setItem('accessToken', 'synthetic-student-token');
    localStorage.setItem('user', JSON.stringify(authUser));
  }, user);
  await page.route('**/api/**', route => reply(route, []));
  await page.route('**/api/auth/session', route => reply(route, { user, authorizationVersion: 1 }));
  await page.route('**/api/public/settings', route => route.fulfill({ json: { maintenanceMode: false } }));
  await page.route('**/api/student/shell-bootstrap', route => reply(route, { hasSeenTrackingCodePopup: true, unreadNotificationsCount: 0, currentBalance: 0, gamification: { totalPoints: 0, levelName: 'طالب' } }));
}

const failedExam: ExamResultDto = { attemptId: 'expired-attempt', scoreAchieved: 9, totalScore: 13, isPassed: false, blocksNextLesson: true, evaluation: 'جيد', isTimeExpired: true, resultState: 'Completed', questions: [] };

test('essay completion refreshes the final result without waiting for the polling timer', async ({ page }) => {
  await studentSession(page);
  await page.clock.install();
  let platformSocket: WebSocketRoute | undefined;
  await page.routeWebSocket('**/hubs/platform**', socket => {
    socket.onMessage(raw => {
      for (const frame of String(raw).split('\x1e').filter(Boolean)) {
        const message = JSON.parse(frame);
        if (message.protocol) {
          socket.send('{}\x1e');
          platformSocket = socket;
        }
        if (message.type === 1 && message.invocationId)
          socket.send(`${JSON.stringify({ type: 3, invocationId: message.invocationId })}\x1e`);
      }
    });
  });
  const pendingResult: ExamResultDto = { ...failedExam, attemptId: 'essay-attempt', resultState: 'Pending' };
  let completed = false;
  let statusRequests = 0;
  await page.route(`**/api/exams/${assessmentId}/latest-result`, route => reply(route, pendingResult));
  await page.route('**/api/exams/attempts/essay-attempt/grading-status', route => {
    statusRequests++;
    return reply(route, { attemptId: pendingResult.attemptId, resultState: completed ? 'Completed' : 'Pending', essays: [] });
  });
  await page.route('**/api/exams/attempts/essay-attempt/result', route => reply(route, { ...pendingResult, resultState: 'Completed', evaluation: 'نتيجة التصحيح الفورية' }));
  await page.goto(`/student/exams/${assessmentId}`);
  await page.getByRole('button', { name: 'نعم، دخول الامتحان', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'بنراجع إجاباتك المقالية الآن', exact: true })).toBeVisible();
  await expect.poll(() => statusRequests).toBeGreaterThan(0);
  await expect.poll(() => Boolean(platformSocket)).toBe(true);
  // Freeze the fallback poll: only the real SignalR handler can refresh the result.
  await page.clock.pauseAt(await page.evaluate(() => Date.now()));
  completed = true;
  platformSocket!.send(`${JSON.stringify({ type: 1, target: 'ExamResultReady', arguments: [JSON.stringify({ examId: assessmentId, attemptId: pendingResult.attemptId, score: 9, isPassed: false })] })}\x1e`);
  await expect(page.getByText('نتيجة التصحيح الفورية', { exact: true })).toBeVisible();
});

function examAttempt(attemptId: string, remainingSeconds = 600): ActiveExamAttemptDto {
  return { attemptId, title: 'امتحان الإعادة', description: '', startedAt: new Date().toISOString(), durationMinutes: 10, remainingSeconds, totalScore: 1, questions: [{ id: 'question-1', text: 'سؤال المحاولة الجديدة', type: 'MCQ', points: 1, options: [{ id: 'option-1', text: 'الإجابة الأولى' }, { id: 'option-2', text: 'الإجابة الثانية' }] }] };
}

// 2026-09-10: restart reloaded the failed result, and clearing an expired result
// before receiving a new attempt could resubmit the old timer automatically.
for (const entry of ['saved-result', 'just-expired'] as const) {
  test(`exam retry starts a new timed attempt from ${entry}`, async ({ page }) => {
    await studentSession(page);
    let starts = 0;
    let oldSubmissions = 0;
    const initialAttempt = examAttempt('expired-attempt', 0);
    const freshAttempt = examAttempt('fresh-attempt');
    await page.route(`**/api/exams/${assessmentId}/latest-result`, route => reply(route, entry === 'saved-result' ? failedExam : null, entry === 'saved-result' ? 200 : 404));
    await page.route(`**/api/exams/${assessmentId}/start`, route => {
      starts++;
      return reply(route, entry === 'just-expired' && oldSubmissions === 0 ? initialAttempt : freshAttempt);
    });
    await page.route(`**/api/exams/${assessmentId}/submit/expired-attempt*`, route => { oldSubmissions++; return reply(route, failedExam); });
    await page.goto(`/student/exams/${assessmentId}`);
  await page.getByRole('button', { name: 'نعم، دخول الامتحان', exact: true }).click();
    const restart = page.getByRole('button', { name: 'إعادة الامتحان', exact: true });
    await expect(restart).toBeVisible();
    const startsBeforeRetry = starts;
    await restart.click();
    await page.getByRole('button', { name: 'إلغاء', exact: true }).click();
    await expect(restart).toBeVisible();
    expect(starts).toBe(startsBeforeRetry);
    await restart.click();
    await page.getByRole('button', { name: 'نعم، دخول الامتحان', exact: true }).click();
    await expect(page.getByText('سؤال المحاولة الجديدة', { exact: true })).toBeVisible();
    await expect(page.getByRole('timer')).toContainText('10');
    await expect(page.getByRole('button', { name: 'إعادة الامتحان', exact: true })).toHaveCount(0);
    expect(starts).toBe(startsBeforeRetry + 1);
    expect(oldSubmissions).toBe(entry === 'just-expired' ? 1 : 0);
  });
}

test('failed restart reports access rejection instead of redisplaying the old result', async ({ page }) => {
  await studentSession(page);
  await page.route(`**/api/exams/${assessmentId}/latest-result`, route => reply(route, failedExam));
  await page.route(`**/api/exams/${assessmentId}/start`, route => route.fulfill({ status: 403, json: { message: 'الحصة غير متاحة لحسابك' } }));
  await page.goto(`/student/exams/${assessmentId}`);
  await page.getByRole('button', { name: 'نعم، دخول الامتحان', exact: true }).click();
  await page.getByRole('button', { name: 'إعادة الامتحان', exact: true }).click();
  await page.getByRole('button', { name: 'نعم، دخول الامتحان', exact: true }).click();
  await expect(page.getByText('الحصة غير متاحة لحسابك', { exact: true })).toBeVisible();
  await expect(page.getByText('سؤال المحاولة الجديدة', { exact: true })).toHaveCount(0);
});

for (const status of ['Graded', 'PendingReview'] as const) {
  test(`homework retry respects ${status} and resets the timed attempt`, async ({ page }) => {
    await studentSession(page);
    let starts = 0;
    let submitted = false;
    const attempt: StartHomeworkAttemptDto = { homeworkId: assessmentId, submissionId: 'homework-first', title: 'واجب الإعادة', totalScore: 1, alreadyCompleted: false, startedAt: new Date().toISOString(), durationMinutes: 10, remainingSeconds: 600, questions: [{ id: 'question-1', order: 1, questionType: 0, text: 'سؤال الواجب', maxPoints: 1, possibleAnswers: ['إجابة أولى', 'إجابة ثانية'] }] };
    const result: HomeworkResultDto = { homeworkId: assessmentId, submissionId: 'homework-first', title: attempt.title, score: 0, totalScore: 1, passingScore: 1, isPassed: false, status, totalQuestions: 1, correctAnswers: 0, wrongAnswers: 1, ungradedAnswers: status === 'PendingReview' ? 1 : 0, questionReviews: [] };
    await page.route(`**/api/homework/${assessmentId}/start`, route => { starts++; return reply(route, { ...attempt, submissionId: submitted ? 'homework-retry' : 'homework-first' }); });
    await page.route(`**/api/homework/${assessmentId}/submit*`, route => { submitted = true; return reply(route, true); });
    await page.route(`**/api/homework/${assessmentId}/result`, route => reply(route, result));
    await page.goto(`/student/homework/${assessmentId}`);
    await page.getByRole('button', { name: 'نعم، دخول الواجب', exact: true }).click();
    await page.getByText('إجابة أولى', { exact: true }).click();
    const initialStarts = starts;
    await page.getByRole('button', { name: 'تسليم الواجب', exact: true }).click();
    const restart = page.getByRole('button', { name: 'إعادة حل الواجب', exact: true });
    if (status === 'PendingReview') {
      await expect(page.getByText('بانتظار التصحيح', { exact: true })).toBeVisible();
      await expect(restart).toHaveCount(0);
      expect(starts).toBe(initialStarts);
    } else {
      await restart.click();
      await page.getByRole('button', { name: 'إلغاء', exact: true }).click();
      expect(starts).toBe(initialStarts);
      await restart.click();
      await page.getByRole('button', { name: 'نعم، دخول الواجب', exact: true }).click();
      await expect(page.getByText('سؤال الواجب', { exact: true })).toBeVisible();
      await expect(page.getByRole('timer')).toContainText('10');
      expect(starts).toBe(initialStarts + 1);
    }
  });
}

test('editing answers does not extend the exam deadline or duplicate timeout submission', async ({ page }) => {
  await studentSession(page);
  await page.clock.install();
  const attempt = examAttempt('timed-attempt', 5);
  let submissions = 0;
  await page.route(`**/api/exams/${assessmentId}/latest-result`, route => reply(route, null, 404));
  await page.route(`**/api/exams/${assessmentId}/start`, route => reply(route, attempt));
  await page.route(`**/api/exams/${assessmentId}/submit/timed-attempt*`, route => { submissions++; return reply(route, failedExam); });
  await page.goto(`/student/exams/${assessmentId}`);
  await page.getByRole('button', { name: 'نعم، دخول الامتحان', exact: true }).click();
  await expect(page.getByText('سؤال المحاولة الجديدة', { exact: true })).toBeVisible();
  await page.clock.runFor(3000);
  await page.getByText('الإجابة الأولى', { exact: true }).click();
  await expect(page.getByRole('timer')).toHaveAttribute('aria-label', 'الوقت المتبقي: 0 دقيقة و2 ثانية');
  await page.clock.runFor(2200);
  await expect(page.getByRole('button', { name: 'إعادة الامتحان', exact: true })).toBeVisible();
  expect(submissions).toBe(1);
});

for (const [routeName, label] of [['exams', 'الامتحان'], ['homework', 'الواجب']] as const) {
  test(`${routeName} entry and cancellation send no start request until confirmation`, async ({ page }) => {
    await studentSession(page);
    let starts = 0;
    await page.route(`**/api/${routeName}/${assessmentId}/latest-result`, route => reply(route, null, 404));
    await page.route(`**/api/${routeName}/${assessmentId}/start`, route => {
      starts++;
      return route.fulfill({ status: 403, json: { message: 'اختبار طلب البدء بعد التأكيد' } });
    });
    await page.goto(`/student/${routeName}/${assessmentId}`);
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText(`هل أنت متأكد من دخول ${label}؟`);
    await page.screenshot({ path: test.info().outputPath('entry-confirmation.png') });
    await expect(dialog.getByRole('button', { name: 'إلغاء', exact: true })).toBeFocused();
    expect(starts).toBe(0);
    await dialog.getByRole('button', { name: 'إلغاء', exact: true }).click();
    await expect(page).toHaveURL(/\/student$/);
    expect(starts).toBe(0);
    await page.goto(`/student/${routeName}/${assessmentId}`);
    await page.getByRole('button', { name: `نعم، دخول ${label}`, exact: true }).click();
    await expect(page.getByRole('heading', { name: `${label} غير متاح`, exact: true })).toBeVisible();
    expect(starts).toBe(1);
  });
}

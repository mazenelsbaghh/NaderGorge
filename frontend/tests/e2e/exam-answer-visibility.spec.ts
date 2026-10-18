import { expect, test, type Page, type Route, type WebSocketRoute } from '@playwright/test';
import { adminUrl, installAuthAndGoto } from './e2e-contract-helpers';
import type { ExamResultDto } from '../../src/services/exam-service';

const examId = '98000000-0000-0000-0000-000000000001';
const result: ExamResultDto = {
  essayScoreStatus: 'Included',
  examId, attemptId: '98000000-0000-0000-0000-000000000002', answersVisible: false,
  scoreAchieved: 7, totalScore: 10, isPassed: true, blocksNextLesson: false,
  evaluation: 'جيد', isTimeExpired: false, resultState: 'Completed',
  questions: [{ examQuestionId: 'question-1', order: 1, questionText: 'سؤال المراجعة المحجوب',
    selectedOptionText: 'إجابة الطالب المحفوظة', correctOptionText: 'الإجابة النموذجية المحجوبة',
    writtenCorrection: 'التعليل المحجوب', isAnswered: true, isCorrect: false, pointsAwarded: 0 }],
};
const reply = (route: Route, data: unknown) => route.fulfill({ json: { success: true, data } });

async function session(page: Page, role: 'Student' | 'Admin') {
  const user = { id: '98000000-0000-0000-0000-000000000099', fullName: 'Answer release test', phone: '20000000001',
    roles: [role], permissions: ['content.manage'], profileComplete: true,
    allowedDomains: [role === 'Admin' ? 'admin' : 'student'], allowedNavbarItems: [], authorizationVersion: 1 };
  await page.route('**/api/**', route => reply(route, []));
  await page.route('**/api/auth/session', route => reply(route, { user, authorizationVersion: 1 }));
  await page.route('**/api/public/settings', route => route.fulfill({ json: { maintenanceMode: false } }));
  await page.route('**/api/student/welcome/claim', route => reply(route, null));
  await page.route('**/api/student/shell-bootstrap', route => reply(route, { hasSeenTrackingCodePopup: true,
    unreadNotificationsCount: 0, currentBalance: 0, gamification: { totalPoints: 0, levelName: 'طالب' } }));
  return user;
}

test('a completed result shows the score alone until the release event refreshes its saved review', async ({ page }) => {
  const user = await session(page, 'Student');
  let socket: WebSocketRoute | undefined;
  await page.routeWebSocket('**/hubs/platform**', route => {
    route.onMessage(raw => {
      for (const frame of String(raw).split('\x1e').filter(Boolean)) {
        const message = JSON.parse(frame);
        if (message.protocol) { route.send('{}\x1e'); socket = route; }
        if (message.type === 1 && message.invocationId)
          route.send(`${JSON.stringify({ type: 3, invocationId: message.invocationId })}\x1e`);
      }
    });
  });
  let visible = false;
  let starts = 0;
  await page.route(`**/api/exams/${examId}/latest-result`, route => reply(route, result));
  await page.route(`**/api/exams/attempts/${result.attemptId}/result`, route => reply(route, { ...result, answersVisible: visible }));
  await page.route(`**/api/exams/${examId}/start`, route => { starts++; return reply(route, {}); });
  await installAuthAndGoto(page, 'synthetic-answer-token', user, `/student/exams/${examId}`);
  await expect(page.getByText('7 / 10', { exact: true })).toBeVisible();
  await expect(page.getByText('الدرجة تشمل المقالي — اكتمل تصحيح الأسئلة المقالية.', { exact: true })).toBeVisible();
  await expect(page.getByText('درجتك محفوظة. سنقوم بإبلاغك عند ظهور الإجابات.', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'الدرجة متاحة، والإجابات لم تُفتح بعد' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'مراجعة الورقة كاملة' })).toHaveCount(0);
  await expect(page.getByText('الإجابة النموذجية المحجوبة', { exact: true })).toHaveCount(0);
  await expect.poll(() => Boolean(socket)).toBe(true);
  visible = true;
  socket!.send(`${JSON.stringify({ type: 1, target: 'ExamResultReady', arguments: [JSON.stringify({
    examId, attemptId: result.attemptId, score: 7, isPassed: true,
  })] })}\x1e`);
  await expect(page.getByRole('heading', { name: 'مراجعة الورقة كاملة' })).toBeVisible();
  await expect(page.getByText('الإجابة النموذجية المحجوبة', { exact: true }).first()).toBeVisible();
  await expect(page.getByText('7 / 10', { exact: true })).toBeVisible();
  expect(starts).toBe(0);
  visible = false;
  socket!.send(`${JSON.stringify({ type: 1, target: 'ExamResultReady', arguments: [JSON.stringify({
    examId, attemptId: result.attemptId, score: 7, isPassed: true,
  })] })}\x1e`);
  await expect(page.getByRole('heading', { name: 'مراجعة الورقة كاملة' })).toHaveCount(0);
  await expect(page.getByText('7 / 10', { exact: true })).toBeVisible();
});

for (const scenario of [
  { status: 'Pending', label: 'الدرجة بدون المقالي — ستُضاف درجته بعد اكتمال التصحيح.', pending: true },
  { status: 'PartiallyIncluded', label: 'الدرجة تشمل المقالي الذي اكتمل تصحيحه فقط، وباقي المقالي قيد التصحيح.', pending: true },
  { status: 'Excluded', label: 'الدرجة بدون المقالي — المقالي غير محتسب في درجة هذا الامتحان.', pending: false },
] as const) {
  test(`the score explains ${scenario.status} essays while answers await release`, async ({ page }) => {
    const user = await session(page, 'Student');
    await page.route(`**/api/exams/${examId}/latest-result`, route => reply(route, {
      ...result, essayScoreStatus: scenario.status, resultState: scenario.pending ? 'PartiallyGraded' : 'Completed',
      partialResult: scenario.pending ? { scoreAchieved: 7, totalScore: 10, pendingQuestionCount: 1 } : null,
    }));
    await page.route(`**/api/exams/attempts/${result.attemptId}/grading-status`, route => reply(route, {
      attemptId: result.attemptId, resultState: 'PartiallyGraded', essays: [],
    }));
    await installAuthAndGoto(page, 'synthetic-answer-token', user, `/student/exams/${examId}`);
    await expect(page.getByText(scenario.label, { exact: true })).toBeVisible();
    await expect(page.getByText('الإجابات النموذجية ظاهرة في المراجعة.', { exact: false })).toHaveCount(0);
    await expect(page.getByText('الدرجة متاحة، والإجابات لم تُفتح بعد', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'مراجعة الورقة كاملة' })).toHaveCount(0);
  });
}

test('the admin switch retains its saved state on failure and reports recipients after successful release', async ({ page }) => {
  const user = await session(page, 'Admin');
  await page.route(`**/api/admin/exams/${examId}/dashboard`, route => reply(route, {
    examId, internalCode: 'EXM-visibility', title: 'امتحان التحكم في الإجابات', description: '',
    questionCount: 0, totalScore: 10, passingScore: 5, isActive: true, showAnswersToStudents: false,
    attempts: [], questions: [], archiveMode: 'None',
  }));
  let fail = true;
  await page.route(`**/api/admin/exams/${examId}/answer-visibility`, route => {
    expect(route.request().postDataJSON()).toEqual({ showAnswers: true });
    return fail ? route.fulfill({ status: 503, json: { success: false } })
      : reply(route, { showAnswersToStudents: true, notifiedStudents: 3 });
  });
  await installAuthAndGoto(page, 'synthetic-admin-token', user, `${adminUrl}/admin/content/exams/${examId}`);
  const control = page.getByRole('switch', { name: 'إظهار الإجابات للطلاب', exact: true });
  await expect(control).toHaveAttribute('aria-checked', 'false');
  await control.click();
  await expect(page.getByRole('alert').filter({ hasText: 'تعذر تغيير ظهور الإجابات' })).toBeVisible();
  await expect(control).toHaveAttribute('aria-checked', 'false');
  fail = false;
  await control.click();
  await expect(control).toHaveAttribute('aria-checked', 'true');
  await expect(page.getByRole('status').filter({ hasText: 'تم إرسال إشعار إلى 3 طالب' })).toBeVisible();
});

test('a release notification links to the saved exam result', async ({ page }) => {
  const user = await session(page, 'Student');
  await page.route('**/api/student/notifications', route => reply(route, [{ id: 'release-notification',
    title: 'إجابات الامتحان متاحة الآن', body: 'راجع نتيجتك المحفوظة.', isRead: false,
    createdAt: new Date().toISOString(), actionUrl: `/student/exams/${examId}?attemptId=${result.attemptId}` }]));
  await page.route(`**/api/exams/${examId}/latest-result`, route => route.fulfill({ status: 500 }));
  await page.route(`**/api/exams/attempts/${result.attemptId}/result`, route => reply(route, { ...result, answersVisible: true }));
  await installAuthAndGoto(page, 'synthetic-answer-token', user, '/student/notifications');
  await page.getByRole('link', { name: 'مراجعة الإجابات', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`attemptId=${result.attemptId}`));
  await expect(page.getByRole('heading', { name: 'مراجعة الورقة كاملة' })).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('a short exam link requires login and returns the account to its saved result', async ({ page }) => {
  const user = await session(page, 'Student');
  await page.route('**/api/auth/session', route => route.fulfill({ status: 401, json: { success: false } }));
  let resolutions = 0;
  let starts = 0;
  await page.route('**/api/public-exams/shamel1', route => {
    resolutions++;
    return reply(route, { examId, slug: 'shamel1' });
  });
  await page.route(`**/api/exams/${examId}/latest-result`, route => reply(route, result));
  await page.route(`**/api/exams/${examId}/start`, route => { starts++; return reply(route, {}); });
  await page.goto('/public-exams/shamel1');
  await expect(page).toHaveURL(/\/login\?returnUrl=/);
  expect(new URL(page.url()).searchParams.get('returnUrl')).toBe('/student/public-exams/shamel1');
  expect(resolutions).toBe(0);
  await page.route('**/api/auth/session', route => reply(route, { user, authorizationVersion: 1 }));
  await page.route('**/api/auth/login', route => reply(route, { user, accessToken: 'synthetic-answer-token' }));
  await page.getByLabel('رقم الهاتف', { exact: true }).fill('20000000001');
  await page.getByLabel('كلمة المرور', { exact: true }).fill('password');
  await page.getByRole('button', { name: 'تسجيل الدخول', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/student/exams/${examId}\\?from=public-exams`));
  await expect(page.getByText('7 / 10', { exact: true })).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(starts).toBe(0);
});

test('an unavailable short link shows an account-specific explanation without starting an exam', async ({ page }) => {
  const user = await session(page, 'Student');
  await page.route('**/api/public-exams/unavailable', route => route.fulfill({ status: 404, json: { success: false } }));
  await installAuthAndGoto(page, 'synthetic-answer-token', user, '/public-exams/unavailable');
  await expect(page.getByRole('heading', { name: 'الامتحان غير متاح', exact: true })).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'تأكد من الرابط وبياناتك الدراسية' })).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'العودة للامتحانات العامة' })).toBeVisible();
});

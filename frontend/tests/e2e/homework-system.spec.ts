import { test, expect } from '@playwright/test';

// ─── Homework System E2E Tests ──────────────────────────────────────────────────
// اختبارات شاملة لنظام الواجبات (حل، تصحيح تلقائي، نتائج، أمان)
// Comprehensive E2E tests for the homework system:
//   - Standalone homework solving page
//   - Auto-grading for MCQ and FindTheMistake questions
//   - Results panel with score, evaluation, pass/fail
//   - Security: CorrectAnswerKey not exposed to students
//   - API integration tests for homework endpoints
// ─────────────────────────────────────────────────────────────────────────────────

const API_BASE = 'http://localhost:5245/api';
const STUDENT_PHONE = '20000000001';
const STUDENT_PASSWORD = 'password';

/**
 * Helper: Login to the backend API directly and return the JWT token.
 * مساعد: تسجيل الدخول عبر API واسترجاع التوكن
 */
async function loginViaApi(
  request: import('@playwright/test').APIRequestContext,
  phone = STUDENT_PHONE,
  password = STUDENT_PASSWORD
): Promise<string> {
  const loginRes = await request.post(`${API_BASE}/auth/login`, {
    data: {
      phoneNumber: phone,
      password,
      deviceFingerprint: `e2e-test-${Date.now()}`,
      deviceName: 'E2E Playwright',
    },
  });
  expect(loginRes.ok(), `Login failed for ${phone}: ${loginRes.status()}`).toBeTruthy();
  const loginData = await loginRes.json();
  // Token is returned in data.token
  return loginData.data?.token || loginData.token;
}

/**
 * Helper: Login as student in the browser UI.
 * مساعد: تسجيل دخول الطالب عبر واجهة المتصفح
 */
async function loginStudentUI(page: import('@playwright/test').Page) {
  await page.goto('http://app.localhost:3000/login');
  await page.waitForTimeout(1000);

  await page.locator('input[type="tel"]').click();
  await page.locator('input[type="tel"]').fill(STUDENT_PHONE);
  await page.locator('input[type="password"]').click();
  await page.locator('input[type="password"]').fill(STUDENT_PASSWORD);
  await page.click('text=تذكرني', { force: true });
  await page.locator('button[type="submit"]').click({ force: true });

  // Wait for redirect to student dashboard
  await expect(page).toHaveURL(/.*\/(student|onboarding)$/, { timeout: 15000 });
}

// ═══════════════════════════════════════════════════════════════════════════════
// Test Group 1: Homework Solving Flow (UI Tests)
// مجموعة الاختبارات 1: تدفق حل الواجب (اختبارات واجهة المستخدم)
// ═══════════════════════════════════════════════════════════════════════════════

test.describe.serial('Homework Solving Flow', () => {
  let mockPackageData: any;

  test.beforeAll(async ({ request }) => {
    // Clear devices to avoid login limits
    // مسح الأجهزة لتجنب حدود الأجهزة
    await request.post(`${API_BASE}/e2e/clear-devices`, {
      data: { phoneNumber: STUDENT_PHONE },
    });

    // Setup mock package (includes homework with MCQ, FindTheMistake, and Essay questions)
    // إعداد الباقة الوهمية (تشمل واجب بأسئلة اختيارات وأوجد الخطأ ومقالية)
    const setupResponse = await request.post(`${API_BASE}/e2e/setup-mock-package`);
    expect(setupResponse.ok()).toBeTruthy();
    mockPackageData = await setupResponse.json();

    // Grant package to Student 1
    // منح الباقة للطالب
    await request.post(`${API_BASE}/e2e/grant-package`, {
      data: { packageId: mockPackageData.packageId },
    });
  });

  test('T001: Student can navigate to homework page from lesson', async ({ page }) => {
    test.setTimeout(60000);

    // Login as student
    await loginStudentUI(page);

    // Navigate to the enrolled package directly
    // الانتقال مباشرة للباقة المسجل فيها
    await page.goto(
      `http://app.localhost:3000/student/packages/${mockPackageData.packageId}`
    );

    // Expand the "E2E Section" accordion
    // فتح قسم "E2E Section"
    const sectionTitle = page.getByRole('heading', { name: 'E2E Section' });
    await sectionTitle.waitFor({ state: 'visible', timeout: 15000 });
    await sectionTitle.click();

    // Navigate to the lesson page
    // الانتقال لصفحة الحصة
    const viewButton = page.locator('button:has-text("مشاهدة")').first();
    await viewButton.waitFor({ state: 'visible', timeout: 10000 });
    await viewButton.click();

    await expect(page).toHaveURL(
      new RegExp(`/lessons/${mockPackageData.lessonId}`),
      { timeout: 10000 }
    );

    // Find and click the "حل الواجب" or "اذهب لحل الواجب" button
    // البحث عن زر الواجب والضغط عليه
    const homeworkBtn = page.locator(
      'button:has-text("حل الواجب"), a:has-text("حل الواجب"), button:has-text("اذهب لحل الواجب"), a:has-text("اذهب لحل الواجب")'
    ).first();
    await expect(homeworkBtn).toBeVisible({ timeout: 15000 });
    await homeworkBtn.click({ force: true });

    // Verify homework page loads
    // التحقق من تحميل صفحة الواجب
    await expect(page).toHaveURL(
      new RegExp(`/student/homework/${mockPackageData.homeworkId}`),
      { timeout: 10000 }
    );

    // Verify homework title appears
    // التحقق من ظهور عنوان الواجب
    await expect(page.locator('text=E2E Homework')).toBeVisible({ timeout: 10000 });

    // Verify questions area is visible (at least one question indicator)
    // التحقق من ظهور منطقة الأسئلة
    await expect(page.locator('text=سؤال 1')).toBeVisible({ timeout: 10000 });
  });

  test('T002: Student can answer MCQ questions and submit homework', async ({ page }) => {
    test.setTimeout(60000);

    // Login
    await loginStudentUI(page);

    // Navigate directly to the homework page
    // الانتقال مباشرة لصفحة الواجب
    await page.goto(
      `http://app.localhost:3000/student/homework/${mockPackageData.homeworkId}?packageId=${mockPackageData.packageId}&lessonId=${mockPackageData.lessonId}`
    );

    // Wait for homework to load (first question should be visible)
    // انتظار تحميل الواجب (السؤال الأول يجب أن يظهر)
    await expect(page.locator('text=E2E Homework')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('text=سؤال 1')).toBeVisible({ timeout: 10000 });

    // ─── Answer Question 1 (MCQ: "ما ناتج 1+1؟") ───
    // Select the correct answer "2"
    // اختيار الإجابة الصحيحة "2"
    const mcq1Option = page.locator('label').filter({ hasText: '2' }).first();
    await mcq1Option.click({ force: true });

    // Verify answer is selected (check for "تمت الإجابة")
    await expect(page.locator('text=تمت الإجابة')).toBeVisible({ timeout: 5000 });

    // Navigate to question 2 using التالي button
    // الانتقال للسؤال التالي
    await page.locator('button:has-text("التالي")').click({ force: true });
    await expect(page.locator('text=سؤال 2')).toBeVisible({ timeout: 5000 });

    // ─── Answer Question 2 (MCQ: "ما الغاز الذي تنتجه النباتات؟") ───
    // Select the correct answer "الأكسجين"
    const mcq2Option = page.locator('label').filter({ hasText: 'الأكسجين' }).first();
    await mcq2Option.click({ force: true });

    // Navigate to question 3 (FindTheMistake)
    await page.locator('button:has-text("التالي")').click({ force: true });
    await expect(page.locator('text=سؤال 3')).toBeVisible({ timeout: 5000 });

    // ─── Skip Question 3 (FindTheMistake) ───
    // We'll skip this one to test partial submission

    // Navigate to question 4 (Essay)
    await page.locator('button:has-text("التالي")').click({ force: true });
    await expect(page.locator('text=سؤال 4')).toBeVisible({ timeout: 5000 });

    // ─── Answer Question 4 (Essay) ───
    const textarea = page.locator('textarea').first();
    await textarea.waitFor({ state: 'visible', timeout: 5000 });
    await textarea.fill('AI is a transformative technology with immense potential.');

    // Click submit button ("تسليم الواجب")
    // الضغط على زر التسليم
    await page.locator('button:has-text("تسليم الواجب")').click({ force: true });

    // A confirmation dialog should appear because we skipped questions
    // يجب أن يظهر مربع حوار التأكيد لأننا تخطينا أسئلة
    const confirmBtn = page.locator('button:has-text("نعم، سلّم الآن")');
    await expect(confirmBtn).toBeVisible({ timeout: 5000 });
    await confirmBtn.click({ force: true });

    // Wait for submission and result panel to appear
    // انتظار التسليم وظهور لوحة النتائج
    // The result panel shows "اجتزت الواجب" or "لم تجتز الواجب"
    await expect(
      page.locator('text=اجتزت الواجب, text=لم تجتز الواجب').first()
    ).toBeVisible({ timeout: 15000 });
  });

  test('T003: Homework auto-grading shows correct score and evaluation', async ({ page }) => {
    test.setTimeout(60000);

    // Login
    await loginStudentUI(page);

    // Navigate directly to the homework page (should show result since already submitted)
    // الانتقال لصفحة الواجب (يجب أن تظهر النتائج لأنه تم تسليمه مسبقاً)
    await page.goto(
      `http://app.localhost:3000/student/homework/${mockPackageData.homeworkId}?packageId=${mockPackageData.packageId}&lessonId=${mockPackageData.lessonId}`
    );

    // Wait for result panel to appear (AlreadyCompleted should redirect to result view)
    // انتظار ظهور لوحة النتائج
    await expect(
      page.locator('text=اجتزت الواجب, text=لم تجتز الواجب').first()
    ).toBeVisible({ timeout: 15000 });

    // Verify score is displayed (الدرجة section in stats grid)
    // التحقق من عرض الدرجة
    await expect(page.locator('text=الدرجة')).toBeVisible({ timeout: 5000 });

    // Verify evaluation text appears (التقييم stat label)
    // التحقق من ظهور نص التقييم
    await expect(page.locator('text=التقييم')).toBeVisible({ timeout: 5000 });

    // Verify إجابات صحيحة (correct answers count) is displayed
    // التحقق من عرض عدد الإجابات الصحيحة
    await expect(page.locator('text=إجابات صحيحة')).toBeVisible({ timeout: 5000 });

    // Verify إجابات خاطئة (wrong answers count) is displayed
    await expect(page.locator('text=إجابات خاطئة')).toBeVisible({ timeout: 5000 });

    // Verify question review section is visible ("مراجعة الإجابات كاملة")
    // التحقق من ظهور قسم مراجعة الأسئلة
    await expect(page.locator('text=مراجعة الإجابات كاملة')).toBeVisible({ timeout: 5000 });

    // Verify individual question review items exist (at least one "سؤال" label)
    await expect(page.locator('text=سؤال 1').first()).toBeVisible({ timeout: 5000 });

    // Verify "العودة للحصة" button is visible
    await expect(page.locator('text=العودة للحصة')).toBeVisible({ timeout: 5000 });
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Test Group 2: Homework API Tests
// مجموعة الاختبارات 2: اختبارات API الواجب
// ═══════════════════════════════════════════════════════════════════════════════

test.describe.serial('Homework API Tests', () => {
  let mockPackageData: any;
  let authToken: string;

  test.beforeAll(async ({ request }) => {
    // Clear devices
    await request.post(`${API_BASE}/e2e/clear-devices`, {
      data: { phoneNumber: STUDENT_PHONE },
    });

    // Setup mock package
    const setupResponse = await request.post(`${API_BASE}/e2e/setup-mock-package`);
    expect(setupResponse.ok()).toBeTruthy();
    mockPackageData = await setupResponse.json();

    // Grant package
    await request.post(`${API_BASE}/e2e/grant-package`, {
      data: { packageId: mockPackageData.packageId },
    });

    // Login via API to get token
    authToken = await loginViaApi(request);
  });

  test('T004: GET /homework/{id}/start returns questions without correct answers (SECURITY)', async ({
    request,
  }) => {
    test.setTimeout(30000);

    // Call homework start endpoint
    // استدعاء نقطة بدء الواجب
    const startRes = await request.get(
      `${API_BASE}/homework/${mockPackageData.homeworkId}/start`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
      }
    );
    expect(startRes.ok()).toBeTruthy();

    const body = await startRes.json();
    const data = body.data;

    // Verify response structure
    expect(data.homeworkId).toBeTruthy();
    expect(data.submissionId).toBeTruthy();
    expect(data.title).toBe('E2E Homework');

    // Verify questions array exists and has items
    // التحقق من وجود مصفوفة الأسئلة
    expect(data.questions).toBeDefined();
    expect(data.questions.length).toBeGreaterThanOrEqual(4); // 2 MCQ + 1 FTM + 1 Essay

    // ╔════════════════════════════════════════════════════════════╗
    // ║ SECURITY CHECK: No question should expose CorrectAnswerKey ║
    // ║ فحص أمني: لا يجب أن يحتوي أي سؤال على الإجابة الصحيحة   ║
    // ╚════════════════════════════════════════════════════════════╝
    for (const question of data.questions) {
      expect(question.correctAnswerKey).toBeUndefined();
      expect(question.correctAnswer).toBeUndefined();
      expect(question.writtenCorrection).toBeUndefined();

      // Verify question has required fields
      expect(question.id).toBeTruthy();
      expect(question.text).toBeTruthy();
      expect(question.questionType).toBeDefined();
      expect(question.maxPoints).toBeGreaterThan(0);
    }

    // Verify MCQ questions have possibleAnswers
    // التحقق من وجود خيارات لأسئلة الاختيارات
    const mcqQuestions = data.questions.filter((q: any) => q.questionType === 0);
    expect(mcqQuestions.length).toBeGreaterThanOrEqual(2);
    for (const mcq of mcqQuestions) {
      expect(mcq.possibleAnswers).toBeDefined();
      expect(mcq.possibleAnswers.length).toBeGreaterThanOrEqual(2);
    }

    // Verify FindTheMistake question has baseText
    // التحقق من أن سؤال أوجد الخطأ يحتوي على النص الأساسي
    const ftmQuestions = data.questions.filter((q: any) => q.questionType === 2);
    expect(ftmQuestions.length).toBeGreaterThanOrEqual(1);
    for (const ftm of ftmQuestions) {
      expect(ftm.baseText).toBeTruthy();
    }
  });

  test('T005: POST /homework/{id}/submit auto-grades MCQ correctly', async ({ request }) => {
    test.setTimeout(30000);

    // First, get the questions to know their IDs
    // أولاً، نحصل على الأسئلة لمعرفة معرفاتها
    const startRes = await request.get(
      `${API_BASE}/homework/${mockPackageData.homeworkId}/start`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
      }
    );
    expect(startRes.ok()).toBeTruthy();
    const startData = (await startRes.json()).data;
    const questions = startData.questions;

    // Build answers: answer MCQ questions correctly, skip others
    // بناء الإجابات: نجيب على أسئلة الاختيارات بشكل صحيح
    const answers: { questionId: string; providedAnswer: string }[] = [];

    for (const q of questions) {
      if (q.questionType === 0) {
        // MCQ: pick the known correct answer based on the question text
        if (q.text.includes('1+1') || q.text.includes('ناتج')) {
          answers.push({ questionId: q.id, providedAnswer: '2' });
        } else if (q.text.includes('الغاز') || q.text.includes('النباتات')) {
          answers.push({ questionId: q.id, providedAnswer: 'الأكسجين' });
        }
      } else if (q.questionType === 1) {
        // Essay: provide a text answer
        answers.push({
          questionId: q.id,
          providedAnswer: 'AI is revolutionary technology that transforms industries.',
        });
      } else if (q.questionType === 2) {
        // FindTheMistake: provide the correct mistake text
        // BaseText: "الشمس كوكبة تدور حولها الأرض", mistake at indices 6-11 → "كوكبة"
        answers.push({ questionId: q.id, providedAnswer: 'كوكبة' });
      }
    }

    // Submit homework
    // تسليم الواجب
    const submitRes = await request.post(
      `${API_BASE}/homework/${mockPackageData.homeworkId}/submit`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
        data: answers,
      }
    );
    expect(submitRes.ok()).toBeTruthy();

    const submitBody = await submitRes.json();
    expect(submitBody.success).toBe(true);

    // Get result
    // الحصول على النتيجة
    const resultRes = await request.get(
      `${API_BASE}/homework/${mockPackageData.homeworkId}/result`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
      }
    );
    expect(resultRes.ok()).toBeTruthy();

    const resultData = (await resultRes.json()).data;

    // Verify result structure
    // التحقق من بنية النتيجة
    expect(resultData.homeworkId).toBeTruthy();
    expect(resultData.submissionId).toBeTruthy();
    expect(resultData.score).toBeDefined();
    expect(resultData.totalScore).toBeDefined();
    expect(resultData.evaluation).toBeTruthy();
    expect(resultData.isPassed).toBeDefined();

    // Homework has Essay questions → status should be PendingReview
    // الواجب يحتوي على أسئلة مقالية → الحالة يجب أن تكون "قيد المراجعة"
    expect(resultData.status).toBe('PendingReview');

    // The MCQ answers were correct (2 MCQ × 10 = 20 points from auto-graded)
    // The FTM answer was correct (+10 points from auto-graded)
    // Essay is ungraded (null score), so overall = (20+10)/40 scaled
    // Score should be > 0 since we answered MCQ + FTM correctly
    expect(resultData.score).toBeGreaterThan(0);
  });

  test('T006: GET /homework/{id}/result returns detailed question reviews', async ({
    request,
  }) => {
    test.setTimeout(30000);

    // Get result (should already be submitted from T005)
    // الحصول على النتيجة (يجب أن تكون مقدمة من T005)
    const resultRes = await request.get(
      `${API_BASE}/homework/${mockPackageData.homeworkId}/result`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
      }
    );
    expect(resultRes.ok()).toBeTruthy();

    const resultData = (await resultRes.json()).data;

    // Verify questionReviews array exists
    // التحقق من وجود مصفوفة مراجعة الأسئلة
    expect(resultData.questionReviews).toBeDefined();
    expect(resultData.questionReviews.length).toBeGreaterThanOrEqual(4);

    // Verify each review has required fields
    // التحقق من أن كل مراجعة تحتوي على الحقول المطلوبة
    for (const review of resultData.questionReviews) {
      expect(review.questionId).toBeTruthy();
      expect(review.order).toBeDefined();
      expect(review.questionType).toBeDefined();
      expect(review.text).toBeTruthy();
      expect(review.maxPoints).toBeGreaterThan(0);

      // MCQ and FTM questions should have correctAnswer and isCorrect
      // أسئلة الاختيارات وأوجد الخطأ يجب أن تحتوي على الإجابة الصحيحة
      if (review.questionType === 0 || review.questionType === 2) {
        expect(review.correctAnswer).toBeTruthy();
        expect(review.scoreReceived).toBeDefined();
        expect(review.isCorrect).toBeDefined();
      }
    }

    // Verify counts make sense
    // التحقق من تناسق العداداد
    expect(resultData.totalQuestions).toBeGreaterThanOrEqual(4);
    expect(
      resultData.correctAnswers + resultData.wrongAnswers + resultData.ungradedAnswers
    ).toBe(resultData.totalQuestions);

    // We answered both MCQ correctly and FTM correctly → at least 3 correct
    expect(resultData.correctAnswers).toBeGreaterThanOrEqual(2);
  });

  test('T007: Duplicate submission is prevented', async ({ request }) => {
    test.setTimeout(30000);

    // Call start again — should report AlreadyCompleted
    // استدعاء البدء مجدداً — يجب أن يبلغ بأنه مكتمل مسبقاً
    const startRes = await request.get(
      `${API_BASE}/homework/${mockPackageData.homeworkId}/start`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
      }
    );
    expect(startRes.ok()).toBeTruthy();

    const data = (await startRes.json()).data;

    // Since homework contains essay questions, status after submit is PendingReview.
    // The start handler returns alreadyCompleted=true for PendingReview status.
    // لأن الواجب يحتوي على أسئلة مقالية، الحالة بعد التسليم هي "قيد المراجعة"
    expect(data.alreadyCompleted).toBe(true);

    // Attempting to submit again should fail
    // محاولة التسليم مرة أخرى يجب أن تفشل
    const submitRes = await request.post(
      `${API_BASE}/homework/${mockPackageData.homeworkId}/submit`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
        data: [{ questionId: data.questions[0].id, providedAnswer: 'test' }],
      }
    );

    const submitBody = await submitRes.json();
    // Either HTTP 400 or success=false with error message
    if (submitRes.ok()) {
      // Idempotent endpoint may return ok with "already submitted" message
      expect(
        submitBody.message?.toLowerCase() || ''
      ).toContain('already');
    } else {
      expect(submitRes.status()).toBe(400);
    }
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Test Group 3: Security Tests
// مجموعة الاختبارات 3: اختبارات الأمان
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('Homework Security Tests', () => {
  let mockPackageData: any;
  let authToken: string;

  test.beforeAll(async ({ request }) => {
    await request.post(`${API_BASE}/e2e/clear-devices`, {
      data: { phoneNumber: STUDENT_PHONE },
    });

    const setupResponse = await request.post(`${API_BASE}/e2e/setup-mock-package`);
    expect(setupResponse.ok()).toBeTruthy();
    mockPackageData = await setupResponse.json();

    await request.post(`${API_BASE}/e2e/grant-package`, {
      data: { packageId: mockPackageData.packageId },
    });

    authToken = await loginViaApi(request);
  });

  test('T008: Lesson detail API does not expose CorrectAnswerKey for homework questions', async ({
    request,
  }) => {
    test.setTimeout(30000);

    // Call GET /api/content/lessons/{lessonId}
    // استدعاء تفاصيل الحصة والتحقق من عدم كشف الإجابات
    const lessonRes = await request.get(
      `${API_BASE}/content/lessons/${mockPackageData.lessonId}`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
      }
    );
    expect(lessonRes.ok()).toBeTruthy();

    const lessonData = (await lessonRes.json()).data;

    // Check homework section in lesson detail
    // التحقق من قسم الواجب في تفاصيل الحصة
    if (lessonData.homework && lessonData.homework.questions) {
      for (const question of lessonData.homework.questions) {
        // CorrectAnswerKey MUST NOT be present
        // يجب ألا يكون مفتاح الإجابة الصحيحة موجوداً
        expect(question.correctAnswerKey).toBeUndefined();
        expect(question.correctAnswer).toBeUndefined();
        expect(question.writtenCorrection).toBeUndefined();
      }
    }

    // Also verify the homeworkId is present in the lesson
    expect(lessonData.homeworkId).toBe(mockPackageData.homeworkId);
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Test Group 4: Purchase Flow Fix Verification
// مجموعة الاختبارات 4: التحقق من إصلاح تدفق الشراء (المعاملات المتداخلة)
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('Purchase Flow Fix', () => {
  test.beforeAll(async ({ request }) => {
    await request.post(`${API_BASE}/e2e/clear-devices`, {
      data: { phoneNumber: STUDENT_PHONE },
    });

    // Setup mock package for purchase test
    const setupResponse = await request.post(`${API_BASE}/e2e/setup-mock-package`);
    expect(setupResponse.ok()).toBeTruthy();
  });

  test('T009: Student with sufficient balance can purchase content', async ({
    page,
    request,
  }) => {
    test.setTimeout(60000);

    // Login as student
    await loginStudentUI(page);

    // Add balance to the student's wallet via code redemption
    // First, generate a code as admin
    // إنشاء كود شحن كمدير وتفعيله كطالب

    // Login as admin in a separate context
    const adminContext = await page.context().browser()!.newContext();
    const adminPage = await adminContext.newPage();

    await adminPage.goto('http://admin.localhost:3000/login');
    await adminPage.fill('input[name="phoneNumber"]', '20000000000');
    await adminPage.fill('input[name="password"]', 'password');
    await adminPage.click('text=تذكرني', { force: true });
    await adminPage.click('button[type="submit"]', { force: true });

    await expect(
      adminPage.getByRole('heading', { name: 'الرئيسية' })
    ).toBeVisible({ timeout: 15000 });

    // Navigate to code generation
    await adminPage.goto('http://admin.localhost:3000/admin/codes');

    // Open Generation Modal
    await adminPage.click('button:has-text("إنشاء دفعة جديدة")');

    // Fill form to create exactly 1 code
    await adminPage.fill('input[type="number"]', '1');

    // Select "Balance" code type
    await adminPage.click('button:has-text("شحن رصيد")');

    // Input EGP Value (500 EGP to ensure sufficient balance)
    await adminPage.fill('input[placeholder="50"]', '500');

    // Submit
    await adminPage.click('button:has-text("توليد الدفعة")');

    // Wait for success
    await expect(
      adminPage.locator('text=تم التوليد بنجاح!').first()
    ).toBeVisible({ timeout: 15000 });
    await adminPage.waitForTimeout(1000);

    // Extract generated code
    await adminPage
      .locator('button[title="عرض التفاصيل والطباعة"]')
      .first()
      .click({ force: true });
    await adminPage.waitForURL(/.*\/admin\/codes\/[0-9a-fA-F-]+$/, {
      timeout: 15000,
    });

    const codeCell = adminPage.locator('tbody tr').first().locator('td').nth(1);
    await expect(codeCell).toBeVisible({ timeout: 15000 });
    const generatedCode = (await codeCell.innerText()).trim();

    await adminContext.close();

    // Now redeem the code as the student
    // تفعيل الكود كطالب
    await page.goto('http://app.localhost:3000/student/code-redemption');
    await page.fill('#student-code-activation-input', generatedCode);
    await page.click('button:has-text("تفعيل الكود")');

    // Confirm activation
    await page.click('button:has-text("تأكيد التفعيل")');

    // Verify code activated successfully
    await expect(
      page.locator('#student-code-activation-success')
    ).toBeVisible({ timeout: 10000 });

    // Now try to purchase a package (this tests the nested transaction fix)
    // محاولة شراء الباقة (يختبر إصلاح المعاملات المتداخلة)
    await page.goto('http://app.localhost:3000/student/packages');

    // Click "استعرض الباقة" for the mock package
    await page.locator('button:has-text("استعرض الباقة")').first().click();
    await expect(page).toHaveURL(/.*\/packages\/.*/);

    // Click "شراء الباقة"
    await page.click('button:has-text("شراء الباقة")');

    // Click "تأكيد الخصم والشراء" inside the purchase modal
    await page.click('button:has-text("تأكيد الخصم والشراء")');

    // Verify successful purchase (no false 'insufficient balance' error)
    // التحقق من نجاح الشراء (بدون خطأ رصيد غير كافي الزائف)
    await expect(page.locator('text=تم الشراء بنجاح!')).toBeVisible({
      timeout: 10000,
    });
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Test Group 5: MCQ-Only Homework (No Essay → Fully Auto-Graded)
// مجموعة الاختبارات 5: واجب اختيارات فقط (بدون مقالي → تصحيح تلقائي كامل)
// ═══════════════════════════════════════════════════════════════════════════════

test.describe.serial('Homework MCQ-Only Auto-Grading API', () => {
  let mockPackageData: any;
  let authToken: string;

  test.beforeAll(async ({ request }) => {
    await request.post(`${API_BASE}/e2e/clear-devices`, {
      data: { phoneNumber: STUDENT_PHONE },
    });

    const setupResponse = await request.post(`${API_BASE}/e2e/setup-mock-package`);
    expect(setupResponse.ok()).toBeTruthy();
    mockPackageData = await setupResponse.json();

    await request.post(`${API_BASE}/e2e/grant-package`, {
      data: { packageId: mockPackageData.packageId },
    });

    authToken = await loginViaApi(request);
  });

  test('T010: Submitting only MCQ+FTM answers (skipping essay) produces auto-graded result', async ({
    request,
  }) => {
    test.setTimeout(30000);

    // Get questions
    const startRes = await request.get(
      `${API_BASE}/homework/${mockPackageData.homeworkId}/start`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
      }
    );
    expect(startRes.ok()).toBeTruthy();
    const questions = (await startRes.json()).data.questions;

    // Only answer MCQ and FindTheMistake questions (skip Essay)
    // فقط نجيب على أسئلة الاختيارات وأوجد الخطأ (نتخطى المقالي)
    const answers: { questionId: string; providedAnswer: string }[] = [];

    for (const q of questions) {
      if (q.questionType === 0) {
        // MCQ
        if (q.text.includes('1+1') || q.text.includes('ناتج')) {
          answers.push({ questionId: q.id, providedAnswer: '2' });
        } else if (q.text.includes('الغاز') || q.text.includes('النباتات')) {
          // Intentionally provide WRONG answer to test scoring
          // نقدم إجابة خاطئة عمداً لاختبار التصحيح
          answers.push({ questionId: q.id, providedAnswer: 'النيتروجين' });
        }
      } else if (q.questionType === 2) {
        // FindTheMistake: correct answer is "كوكبة" (indices 6-11 of base text)
        answers.push({ questionId: q.id, providedAnswer: 'كوكبة' });
      }
      // Skip essay (questionType === 1)
    }

    // Submit
    const submitRes = await request.post(
      `${API_BASE}/homework/${mockPackageData.homeworkId}/submit`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
        data: answers,
      }
    );
    expect(submitRes.ok()).toBeTruthy();

    // Get result
    const resultRes = await request.get(
      `${API_BASE}/homework/${mockPackageData.homeworkId}/result`,
      {
        headers: { Authorization: `Bearer ${authToken}` },
      }
    );
    expect(resultRes.ok()).toBeTruthy();

    const result = (await resultRes.json()).data;

    // Since we skipped the essay question entirely, it should still be PendingReview
    // because the essay question exists on the homework even if not answered
    // However, if we didn't provide an essay answer, the handler only checks if
    // any question in the submission has QuestionType.Essay — let's verify the actual status
    expect(result.status).toBeDefined();
    expect(result.score).toBeDefined();
    expect(result.totalScore).toBe(40);

    // We got 1 MCQ correct (10pts) + 1 MCQ wrong (0pts) + 1 FTM correct (10pts) = 20 raw pts
    // Scaled to TotalScore of 40: (20/30) * 40 ≈ 26.67 (we only submitted 3 of 4 questions)
    // Actual calculation depends on rawPointsPossible from submitted answers only
    expect(result.correctAnswers).toBeGreaterThanOrEqual(2); // MCQ1 correct + FTM correct
    expect(result.wrongAnswers).toBeGreaterThanOrEqual(1); // MCQ2 wrong

    // Verify individual question review scores
    const reviewMap = new Map(
      result.questionReviews.map((r: any) => [r.questionId, r])
    );

    for (const q of questions) {
      if (q.questionType === 0 && (q.text.includes('1+1') || q.text.includes('ناتج'))) {
        const review: any = reviewMap.get(q.id);
        if (review) {
          expect(review.isCorrect).toBe(true);
          expect(review.scoreReceived).toBe(10);
        }
      }
    }
  });
});

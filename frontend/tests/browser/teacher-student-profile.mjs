import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { chromium } from '@playwright/test';

const origin = process.env.TEACHER_PROFILE_TEST_ORIGIN ?? 'http://teacher.lvh.me:8771';
const studentId = 'b913ac03-5369-4d17-91d3-1da2327d0780';
const courseId = 'b913ac03-5369-4d17-91d3-1da2327d0781';
const profile = {
  id: studentId, fullName: 'أحمد محمد', phone: '01000000001', parentPhone: '01000000002', studentCode: 'ST-21',
  grade: 'SecondBaccalaureate', educationStage: 'Baccalaureate', schoolName: 'مدرسة الطالب', governorate: 'القاهرة', isActive: true, createdAt: '2026-10-01T12:00:00Z',
  packages: [{ id: courseId, accessGrantId: 'grant-1', name: 'كورس التاريخ', enrolledAt: '2026-10-01T12:00:00Z', isActive: true, purchaseMethod: 'Code', paidAmount: 150 }],
  watchTracking: { totalActualWatchedSeconds: 65, activities: [{ lessonVideoId: 'video-1', lessonId: 'lesson-1', lessonTitle: 'الحصة الأولى', videoTitle: 'شرح الدرس', packageName: 'كورس التاريخ',
    watchCount: 1, maxWatchCount: 3, watchedSeconds: 65, actualWatchedSeconds: 65, learningWatchedSeconds: 65, durationSeconds: 100, isCompleted: false, averagePlaybackRate: 1, lastWatchedAt: '2026-10-28T10:00:00Z',
    sessions: [{ id: 'session-1', startedAt: '2026-10-28T10:00:00Z', actualWatchedSeconds: 65 }] }] },
  examHistory: [{ attemptId: 'attempt-1', examId: 'exam-1', title: 'امتحان الحصة', packageName: 'كورس التاريخ', lessonTitle: 'الحصة الأولى', score: 8, totalScore: 10, hasFinalGrade: true, isPassed: true, status: 'Graded', attemptedAt: '2026-10-28T10:00:00Z' }],
  homeworkHistory: [{ submissionId: 'submission-1', homeworkId: 'homework-1', title: 'واجب الحصة', score: 0, totalScore: 10, hasFinalGrade: false, status: 'PendingReview', attemptedAt: '2026-10-28T10:00:00Z' }],
  promotionalBalances: [{ availableAmount: 50 }], balanceTransactions: [], rechargeRequests: [], overrides: [], auditTrail: [],
  notes: [{ id: 'note-1', content: 'يحتاج إلى مراجعة الحصة الثانية', adminName: 'المدرس', createdAt: '2026-10-28T10:00:00Z' }],
};

for (const [width, finance] of [[390, true], [1280, false]]) {
  test(`teacher opens a student file, reviews history and preserves scoped permissions at ${width}px`, { timeout: 120000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width, height: 950 } });
      const user = { id: 'teacher-test', fullName: 'المدرس', roles: ['Teacher'], permissions: ['exams.manage', 'content.manage'], allowedDomains: ['teacher'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
      await page.addInitScript(user => { localStorage.setItem('accessToken', 'synthetic-teacher-token'); localStorage.setItem('user', JSON.stringify(user)); }, user);
      const adminStudentRequests = [], studentChanges = [];
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname;
        if (path.startsWith('/api/admin/users/') || path.startsWith('/api/admin/exams/')) adminStudentRequests.push(path);
        if (path.includes(`/teacher/students/${studentId}`) && route.request().method() !== 'GET') studentChanges.push(path);
        if (path.endsWith('/auth/session')) return route.fulfill({ json: { success: true, data: { user, authorizationVersion: 1 } } });
        if (path.endsWith('/teacher/context')) return route.fulfill({ json: { success: true, data: { isOwner: finance, permissionKeys: ['students', 'reports'] } } });
        if (path.endsWith(`/teacher/students/${studentId}/profile`)) return route.fulfill({ json: { success: true, data: { profile, canViewFinance: finance, canViewReports: true } } });
        if (path.endsWith('/teacher/students')) return route.fulfill({ json: { success: true, data: [{ id: studentId, fullName: profile.fullName, phoneNumber: profile.phone, activatedPackageName: 'كورس التاريخ', activatedAt: profile.createdAt, activePackageCount: 1, activeGrantCount: 1, educationStage: profile.educationStage, gradeLevel: profile.grade }] } });
        return route.fulfill({ json: { success: true, data: [] } });
      });
      await page.goto(`${origin}/teacher/students`);
      const studentLink = page.getByRole('link', { name: 'عرض ملف الطالب أحمد محمد', exact: true });
      await studentLink.locator('svg').click();
      await page.getByRole('heading', { name: 'ملف الطالب', exact: true }).waitFor();
      await page.getByRole('heading', { name: 'الحصة الأولى', exact: true }).waitFor();
      await page.getByRole('heading', { name: 'شرح الدرس', exact: true }).waitFor();
      await page.getByText('بدأ ولم يكمل · 65%', { exact: true }).waitFor();
      await page.getByText('جلسات المشاهدة (1)', { exact: true }).click();
      await page.getByRole('tab', { name: 'بيانات الطالب', exact: true }).click();
      await page.getByRole('heading', { name: 'البيانات الشخصية والدراسية', exact: true }).waitFor();
      await page.getByText('مدرسة الطالب', { exact: true }).waitFor();
      await page.getByText('القاهرة', { exact: true }).waitFor();
      await page.getByText(profile.parentPhone, { exact: true }).waitFor();
      assert.deepEqual(await page.getByRole('tab').allTextContents(), ['المشاهدة', 'الدرجات', 'بيانات الطالب', 'الاشتراكات']);
      await page.getByRole('tab', { name: 'الاشتراكات', exact: true }).click();
      await page.getByRole('heading', { name: 'كورس التاريخ', exact: true }).waitFor();
      assert.equal(await page.getByText('المبلغ المدفوع', { exact: true }).count(), finance ? 1 : 0);
      await page.getByRole('tab', { name: 'المشاهدة', exact: true }).click();
      await page.getByRole('heading', { name: 'شرح الدرس', exact: true }).waitFor();
      const screenshots = '/tmp/codex-lesson-reports-screenshots'; await mkdir(screenshots, { recursive: true });
      await page.screenshot({ path: `${screenshots}/teacher-profile-${width}.png`, fullPage: true, animations: 'disabled' });
      await page.getByRole('tab', { name: 'الدرجات', exact: true }).click();
      await page.getByText('امتحان الحصة', { exact: true }).filter({ visible: true }).waitFor();
      await page.getByText('واجب الحصة', { exact: true }).filter({ visible: true }).waitFor();
      await page.getByText('8', { exact: true }).filter({ visible: true }).waitFor();
      await page.getByText('بانتظار التصحيح', { exact: true }).filter({ visible: true }).waitFor();
      assert.equal(await page.getByRole('button', { name: /حذف|تعديل|حفظ/ }).count(), 0);
      assert.deepEqual(adminStudentRequests, []);
      assert.deepEqual(studentChanges, []);
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1), false);
    } finally { await browser.close(); }
  });
}

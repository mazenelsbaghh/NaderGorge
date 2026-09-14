import { expect, test, type Page } from '@playwright/test';
import { installAuthAndGoto } from './e2e-contract-helpers';

const studentId = '97000000-0000-0000-0000-000000000088';
const exam = { attemptId: 'exam-attempt', examId: 'exam-1', title: 'امتحان التاريخ', score: 12, totalScore: 13, hasFinalGrade: true, isPassed: true, isTimeExpired: false, status: 'Graded', attemptedAt: '2026-09-12T10:00:00Z' };
const homework = { submissionId: 'homework-attempt', homeworkId: 'homework-1', title: 'واجب التاريخ', score: 0, totalScore: 10, hasFinalGrade: false, status: 'InProgress', attemptedAt: '2026-09-12T11:00:00Z' };

async function openHistory(page: Page, baseURL: string, permissions?: string[]) {
  const user = { id: 'history-operator', fullName: 'مشرف تجريبي', roles: permissions ? ['Assistant'] : ['Admin'], permissions: permissions ?? [], phone: '20000000999', profileComplete: true, allowedDomains: ['admin'], allowedNavbarItems: [], authorizationVersion: 1 };
  await page.route('**/api/**', route => {
    const pathname = new URL(route.request().url()).pathname;
    const response = pathname.endsWith('/auth/session') ? { user, authorizationVersion: 1 }
      : pathname.endsWith(`/admin/users/students/${studentId}/profile`) ? {
        id: studentId, fullName: 'طالب تجريبي', phone: '20000000001', isActive: true, createdAt: '2026-09-01T00:00:00Z',
        packages: [], devices: [], overrides: [], currentBalance: 0, examHistory: [exam], homeworkHistory: [homework],
        watchTracking: { totalWatchedSeconds: 0, totalActualWatchedSeconds: 0, averagePlaybackRate: 1, watchedVideosCount: 0, activities: [] },
      } : [];
    return route.fulfill({ json: { success: true, data: response } });
  });
  await installAuthAndGoto(page, 'synthetic-admin-token', user, `${baseURL.replace('app.lvh.me', 'admin.lvh.me')}/admin/users/${studentId}`);
  await page.getByRole('tab', { name: 'الأكاديمية', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'سجل الامتحانات', exact: true })).toBeVisible();
}

for (const kind of ['exam', 'homework'] as const) {
  test(`${kind} history deletion confirms the exact attempt and retains it after a failed request`, async ({ page, baseURL }) => {
    await openHistory(page, baseURL!);
    const title = kind === 'exam' ? exam.title : homework.title;
    const section = page.locator('section').filter({ has: page.getByRole('heading', { name: kind === 'exam' ? 'سجل الامتحانات' : 'سجل الواجبات', exact: true }) });
    const deleteButton = section.getByRole('button', { name: new RegExp(`حذف محاولة ${title}`) });
    let requests = 0;
    let releaseDelete!: () => void;
    const deleteGate = new Promise<void>(resolve => { releaseDelete = resolve; });
    const endpoint = kind === 'exam' ? '/admin/exams/exam-1/attempts/exam-attempt' : '/admin/homework/homework-1/submissions/homework-attempt';
    await page.route(`**/api${endpoint}`, async route => {
      expect(route.request().method()).toBe('DELETE');
      requests++;
      if (requests === 1) return route.fulfill({ status: 500, json: { success: false, message: 'تعذر حذف المحاولة الآن.' } });
      await deleteGate;
      return route.fulfill({ json: { success: true, data: true } });
    });
    await deleteButton.click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('طالب تجريبي');
    await expect(dialog).toContainText(title);
    await dialog.getByRole('button', { name: 'إلغاء', exact: true }).click();
    expect(requests).toBe(0);
    await expect(deleteButton).toBeVisible();
    await deleteButton.click();
    await dialog.getByRole('button', { name: 'حذف المحاولة نهائيًا', exact: true }).click();
    await expect(dialog.getByRole('button', { name: 'حذف المحاولة نهائيًا', exact: true })).toBeEnabled();
    expect(requests).toBe(1);
    await dialog.getByRole('button', { name: 'إلغاء', exact: true }).click();
    await expect(deleteButton).toBeVisible();
    await section.screenshot({ path: test.info().outputPath('history-delete-button.png') });
    await deleteButton.click();
    await dialog.screenshot({ path: test.info().outputPath('history-delete-confirmation.png') });
    await dialog.getByRole('button', { name: 'حذف المحاولة نهائيًا', exact: true }).click();
    await expect(dialog.getByRole('button', { name: 'جارٍ التنفيذ...', exact: true })).toBeDisabled();
    await expect(dialog.getByRole('button', { name: 'إلغاء', exact: true })).toBeDisabled();
    releaseDelete();
    await expect(dialog).toHaveCount(0);
    await expect(deleteButton).toHaveCount(0);
    await expect(section).toContainText('0 محاولة');
    await expect(page.getByText(kind === 'exam' ? homework.title : exam.title, { exact: true }).filter({ visible: true })).toBeVisible();
    expect(requests).toBe(2);
  });
}

test('a profile reader cannot see attempt deletion actions without assessment permissions', async ({ page, baseURL }) => {
  await openHistory(page, baseURL!, ['users.manage']);
  await expect(page.getByText(homework.title, { exact: true }).filter({ visible: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /حذف محاولة/ })).toHaveCount(0);
});

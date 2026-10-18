import { expect, test, type Page } from '@playwright/test';
import { installAuthAndGoto, staffUrl } from './e2e-contract-helpers';

const root = '/assistant/public-exams';
const productId = '94000000-0000-0000-0000-000000000001';
const examId = '94000000-0000-0000-0000-000000000002';
const title = 'امتحان عام لاختبار صلاحية الموظف';

async function session(page: Page, permission = true, navigation = true) {
  const user = {
    id: '94000000-0000-0000-0000-000000000003', fullName: 'Staff exam regression', phone: '20000000003',
    roles: ['Staff'], permissions: ['content.manage', 'exams.manage', ...(permission ? ['public_exams.manage'] : [])],
    allowedDomains: ['assistant'], allowedNavbarItems: ['/assistant/dashboard', '/assistant/content', ...(navigation ? [root] : [])],
    profileComplete: true, authorizationVersion: 1,
  };
  await page.route('**/api/**', route => route.fulfill({ json: { success: true, data: [] } }));
  await page.route('**/api/auth/session', route => route.fulfill({ json: { success: true, data: { user, authorizationVersion: 1 } } }));
  await page.route('**/api/public/settings', route => route.fulfill({ json: { maintenanceMode: false } }));
  await page.route('**/api/admin/public-exams', route => route.fulfill({ json: { success: true, data: [{
    id: productId, examId, examTitle: title, slug: 'staff-test', isPublished: false, isPaid: false, price: 0, isPlatformWide: true,
  }] } }));
  await page.route(`**/api/admin/exams/${examId}/dashboard`, route => route.fulfill({ json: { success: true, data: {
    examId, title, internalCode: 'STAFF-TEST', totalScore: 10, passingScore: 5,
    questionCount: 0, showAnswersToStudents: false, questions: [], attempts: [],
  } } }));
  await page.route(`**/api/admin/public-exams/${productId}/results`, route => route.fulfill({ json: { success: true, data: {
    examTitle: title, attemptCount: 0, passedCount: 0, averageScore: 0, isPaid: false, price: 0, attempts: [], questions: [],
  } } }));
  return user;
}

test('staff public-exam permission opens the list, results and editor inside the staff workspace (2026-10-18)', async ({ page }) => {
  const user = await session(page);
  await installAuthAndGoto(page, 'synthetic-staff-token', user, `${staffUrl}/assistant/dashboard`);
  await page.getByRole('button', { name: 'التعليم والمحتوى', exact: true }).click();
  await page.getByRole('link', { name: 'الامتحانات العامة', exact: true }).click();
  await expect(page).toHaveURL(`${staffUrl}${root}`);
  await expect(page.getByText(title, { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'النتائج', exact: true }).click();
  await expect(page).toHaveURL(`${staffUrl}${root}/${productId}/results`);
  await expect(page.getByRole('heading', { name: title })).toBeVisible();
  await page.getByRole('link', { name: 'رجوع للامتحانات العامة' }).click();
  await page.getByRole('link', { name: 'فتح وإضافة أسئلة' }).click();
  await expect(page).toHaveURL(`${staffUrl}${root}/${productId}`);
  await expect(page.getByRole('heading', { name: title })).toBeVisible();
  await page.getByRole('button', { name: 'تعديل الإعدادات والأسئلة', exact: true }).click();
  await expect(page).toHaveURL(`${staffUrl}/assistant/content/exams/${examId}/add-question`);
});

for (const denied of [{ permission: false, navigation: true }, { permission: true, navigation: false }]) {
  test(`staff denied by ${denied.permission ? 'navigation' : 'permission'} cannot see or open public exams`, async ({ page }) => {
    const user = await session(page, denied.permission, denied.navigation);
    await installAuthAndGoto(page, 'synthetic-staff-token', user, `${staffUrl}/assistant/dashboard`);
    await expect(page.getByRole('heading', { name: 'لوحة المساعد', exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: 'الامتحانات العامة', exact: true })).toHaveCount(0);
    let reads = 0;
    page.on('request', request => { if (request.url().endsWith('/api/admin/public-exams')) reads++; });
    await page.goto(`${staffUrl}${root}`);
    await expect(page.getByText('404', { exact: true }).first()).toBeVisible();
    expect(reads).toBe(0);
  });
}

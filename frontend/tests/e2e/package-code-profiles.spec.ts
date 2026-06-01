import { expect, test } from '@playwright/test';

const apiBaseUrl = 'http://localhost:5245/api';

async function loginAs(page: import('@playwright/test').Page, phone: string) {
  await page.goto('/login');
  await page.fill('input[type="tel"]', phone);
  await page.fill('input[type="password"]', 'password');
  await page.click('button[type="submit"]');
}

test.describe('Package code page profiles', () => {
  let packageId: string;
  let secondPackageId: string;

  test.beforeEach(async ({ request }) => {
    await request.post(`${apiBaseUrl}/e2e/clear-devices`, {
      data: { phoneNumber: '20000000000' },
    });
    await request.post(`${apiBaseUrl}/e2e/clear-devices`, {
      data: { phoneNumber: '20000000001' },
    });

    const setupResponse = await request.post(`${apiBaseUrl}/e2e/setup-mock-package`);
    expect(setupResponse.ok()).toBeTruthy();
    const setupJson = await setupResponse.json();
    packageId = setupJson.packageId;

    const secondSetupResponse = await request.post(`${apiBaseUrl}/e2e/setup-mock-package`);
    expect(secondSetupResponse.ok()).toBeTruthy();
    const secondSetupJson = await secondSetupResponse.json();
    secondPackageId = secondSetupJson.packageId;
  });

  test('admin can save and reopen a package code profile without leaking to another package', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    await loginAs(page, '20000000000');
    await expect(page).toHaveURL(/\/admin/, { timeout: 15000 });

    await page.goto(`/admin/content/packages/${packageId}`);
    await page.getByRole('button', { name: 'صفحة الأكواد' }).click();

    await expect(page.getByText('إعدادات صفحة الأكواد')).toBeVisible();
    await page.locator('select').first().selectOption('Published');
    await page.getByLabel('العنوان الرئيسي').fill('عنوان مخصص للأولى');
    await page.getByLabel('وصف العرض').fill('هذا وصف مخصص للحزمة الأولى فقط.');
    await page.getByRole('button', { name: 'حفظ البروفايل' }).click();

    await expect(page.getByText('تم حفظ بروفايل صفحة الكود بنجاح.')).toBeVisible();
    await page.reload();
    await page.getByRole('button', { name: 'صفحة الأكواد' }).click();
    await expect(page.getByLabel('العنوان الرئيسي')).toHaveValue('عنوان مخصص للأولى');

    await page.goto(`/admin/content/packages/${secondPackageId}`);
    await page.getByRole('button', { name: 'صفحة الأكواد' }).click();
    await expect(page.getByLabel('العنوان الرئيسي')).not.toHaveValue('عنوان مخصص للأولى');

    await context.close();
  });

  test('admin validation/reset flow works and student sees the published custom page', async ({ browser }) => {
    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();

    await loginAs(adminPage, '20000000000');
    await expect(adminPage).toHaveURL(/\/admin/, { timeout: 15000 });

    await adminPage.goto(`/admin/content/packages/${packageId}`);
    await adminPage.getByRole('button', { name: 'صفحة الأكواد' }).click();

    await adminPage.locator('select').first().selectOption('Published');
    await adminPage.getByLabel('العنوان الرئيسي').fill('');
    await adminPage.getByRole('button', { name: 'حفظ البروفايل' }).click();
    await expect(adminPage.getByText('Hero title is required for publishing.')).toBeVisible();

    await adminPage.getByLabel('العنوان الرئيسي').fill('عنوان الطالب المخصص');
    await adminPage.getByLabel('الوصف الرئيسي').fill('هذه نسخة مخصصة يراها الطالب.');
    await adminPage.getByLabel('وصف العرض').fill('المحتوى سيفتح مباشرة بعد التفعيل.');
    await adminPage.getByLabel('وصف التفعيل').fill('اكتب الكود الخاص بهذه الباقة الآن.');
    await adminPage.getByRole('button', { name: 'حفظ البروفايل' }).click();
    await expect(adminPage.getByText('تم حفظ بروفايل صفحة الكود بنجاح.')).toBeVisible();

    const context = await browser.newContext();
    const page = await context.newPage();

    await loginAs(page, '20000000001');
    await expect(page).toHaveURL(/\/student/, { timeout: 15000 });

    await page.goto(`/student/code-redemption/packages/${packageId}`);

    await expect(page).toHaveURL(new RegExp(`/student/code-redemption/packages/${packageId}`));
    await expect(page.getByRole('heading', { name: 'عنوان الطالب المخصص' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'ماذا بعد التفعيل؟' })).toBeVisible();

    await adminPage.bringToFront();
    await adminPage.getByRole('button', { name: 'إعادة للوضع الافتراضي' }).click();
    await expect(adminPage.getByText('تمت إعادة الصفحة إلى الوضع الافتراضي.')).toBeVisible();

    await context.close();
    await adminContext.close();
  });
});

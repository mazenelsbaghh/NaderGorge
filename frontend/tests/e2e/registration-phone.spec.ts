import { expect, test } from '@playwright/test';

for (const [scenario, phone, normalized] of [
  ['plain screenshot number', '01010834084', '01010834084'],
  ['copied Arabic number with invisible direction marks', '\u202a٠١٠١٠٨٣٤٠٨٤\u202c\u200f ', '01010834084'],
  ['international formatted number', '+20 (10) 1083-4084', '01010834084'],
  ['optional empty number', '', ''],
]) {
  test(`guardian registration accepts ${scenario} while rejecting incomplete numbers`, async ({ page }) => {
    await page.route('**/api/**', route => route.fulfill({ json: { success: true, data: [] } }));
    await page.goto('/register');
    await page.getByRole('dialog').getByRole('button', { name: 'إغلاق', exact: true }).click();
    await page.getByRole('radiogroup', { name: 'اختر الأفاتار الخاص بك' }).getByRole('radio').first().click();
    await page.locator('#reg-fullName').fill('أحمد محمد محمود علي');
    await page.locator('#reg-phone').fill('01012345678');
    await page.locator('#reg-dob').fill('2010-01-01');
    await page.locator('#reg-gender').selectOption('Male');
    await page.locator('#reg-nationality').selectOption({ index: 1 });
    await page.locator('#reg-governorate').selectOption('القاهرة');
    await page.locator('#reg-district').selectOption({ index: 1 });
    await page.locator('#reg-address').fill('عنوان الطالب التجريبي');
    await page.getByRole('button', { name: 'التالي', exact: true }).click();
    await page.locator('#reg-parentPhone').fill('01112345678');
    await page.locator('#reg-fatherDob').fill('1980-01-01');
    await page.locator('#reg-motherPhone').fill('01212345678');
    await page.locator('#reg-motherDob').fill('1982-01-01');
    const extraPhone = page.locator('#reg-secondaryParentPhone');
    await extraPhone.fill('0101083408');
    await page.getByRole('button', { name: 'التالي', exact: true }).click();
    await expect(page.getByText('تأكد من كتابة رقم ولي أمر إضافي بشكل صحيح', { exact: true })).toBeVisible();
    await extraPhone.fill(phone);
    await expect(extraPhone).toHaveValue(normalized);
    await page.getByRole('button', { name: 'التالي', exact: true }).click();
    await expect(page.locator('#reg-schoolName')).toBeVisible();
    await expect(page.getByText('تأكد من كتابة رقم ولي أمر إضافي بشكل صحيح', { exact: true })).toHaveCount(0);
  });
}

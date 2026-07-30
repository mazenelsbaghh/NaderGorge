import { test, expect } from '@playwright/test';
import {
  generateStudentContext,
} from '../fixtures/auth-helpers';

test.describe('US1: Codes & Wallet Lifecycle', () => {
  const student = generateStudentContext();
  let generatedCode = '';

  test.beforeAll(async ({ request }) => {
    // Destroy and recreate E2E DB state + seed teacher fallback and mock package
    const setupResponse = await request.post(
      'http://localhost:5245/api/e2e/setup-mock-package'
    );
    expect(setupResponse.ok()).toBeTruthy();

    await request.post('http://localhost:5245/api/e2e/clear-devices', {
      data: { phoneNumber: '20000000001' },
    });
  });

  test('T008: Admin Bulk Generation generates a 500 EGP balance code', async ({
    browser,
  }) => {
    test.setTimeout(60000);
    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();

    // Admin login
    await adminPage.goto('http://admin.localhost:3000/login');

    // Fill admin phone and password (seeded from E2E Controller)
    await adminPage.fill('input[name="phoneNumber"]', '20000000000');
    await adminPage.fill('input[name="password"]', 'password');
    await adminPage.click('text=تذكرني', { force: true });
    await adminPage.click('button[type="submit"]', { force: true });

    // Wait for dashboard Navigation
    await expect(adminPage.getByRole('heading', { name: 'الرئيسية' })).toBeVisible({
      timeout: 15000,
    });

    // Navigate to code generation
    await adminPage.goto('http://admin.localhost:3000/admin/codes');
    
    // Open Generation Modal
    await adminPage.click('button:has-text("إنشاء دفعة جديدة")');

    // Fill form to create exactly 1 code
    await adminPage.fill('input[type="number"]', '1');

    // Select "Balance" code type from custom grid
    await adminPage.click('button:has-text("شحن رصيد")');

    // Input EGP Value
    await adminPage.fill('input[placeholder="50"]', '500');

    // Submit
    await adminPage.click('button:has-text("توليد الدفعة")');

    // Assuming the system redirects to code details or shows the code
    await expect(
      adminPage.locator('text=تم التوليد بنجاح!').first()
    ).toBeVisible({ timeout: 15000 });
    await adminPage.waitForTimeout(1000);

    // Navigate to code details to extract the generated code
    await adminPage.locator('button[title="عرض التفاصيل والطباعة"]').first().click({ force: true });
    await adminPage.waitForURL(/.*\/admin\/codes\/[0-9a-fA-F-]+$/, { timeout: 15000 });

    // Extract generated code from table
    const codeCell = adminPage.locator('tbody tr').first().locator('td').nth(1);
    await expect(codeCell).toBeVisible({ timeout: 15000 });
    generatedCode = (await codeCell.innerText()).trim();
    console.log('--- EXTRACTED CODE FROM UI:', generatedCode);

    await adminContext.close();
  });

  test('T009 & T010 & T011: Student Registration, Code Redemption, and Wallet Purchase', async ({
    page,
  }) => {
    test.setTimeout(60000);
    await page.goto('http://app.localhost:3000/register');

    // Dismiss instructions modal
    await page.click('button:has-text("فهمت وموافق على الشروط")');
    await page.waitForTimeout(500);

    // Step 1: Identity
    await page.fill(
      'input[name="fullName"]',
      `Student ${student.randomSuffix} test test`
    );
    await page.fill('input[name="phoneNumber"]', student.phone);
    await page.fill('input[name="dateOfBirth"]', '2005-06-15');
    await page.selectOption('select[name="gender"]', 'Male');
    await page.selectOption('select[name="nationality"]', 'مصري');
    await page.selectOption('select[name="governorate"]', 'القاهرة');
    await page.selectOption('select[name="district"]', { index: 1 });
    await page.fill('input[name="address"]', '123 Test St');
    await page.click('button:has-text("التالي")');

    // Step 2: Guardian
    await page.fill('input[name="parentPhone"]', '010123456' + student.randomSuffix, { force: true });
    await page.fill('input[name="fatherDateOfBirth"]', '1970-01-01', { force: true });
    await page.fill('input[name="motherPhone"]', '010876543' + student.randomSuffix, { force: true });
    await page.fill('input[name="motherDateOfBirth"]', '1975-01-01', { force: true });
    await page.fill('input[name="secondaryParentPhone"]', '01011112222', { force: true });
    await page.click('button:has-text("التالي")', { force: true });

    // Step 3: Academic
    await expect(page.locator('select[name="educationStage"]')).toBeVisible();
    await page.fill('input[name="schoolName"]', 'E2E School');
    await page.selectOption('select[name="schoolType"]', { index: 1 });
    await page.selectOption('select[name="educationStage"]', 'Secondary', { force: true });
    await page.selectOption('select[name="gradeLevel"]', 'FirstSecondary', { force: true });
    await page.click('button:has-text("التالي")', { force: true });

    // Step 4: Security
    await expect(page.locator('input[name="password"]')).toBeVisible();
    await page.fill('input[name="password"]', 'TestPass123!', { force: true });
    await page.fill('input[name="confirmPassword"]', 'TestPass123!', { force: true });
    
    // Submit final registration step
    await page.click('button[type="submit"]', { force: true });
    
    // Wait for login redirection directly to student portal
    await expect(page).toHaveURL(/.*\/student/, { timeout: 15000 });

    // Dismiss terms and conditions dialog
    const termsBtn = page.locator('button:has-text("أوافق وأرغب في استكمال استخدام المنصة")');
    await expect(termsBtn).toBeVisible({ timeout: 10000 });
    await termsBtn.click({ force: true });

    // 2. Navigate to Wallet/Code Redemption page
    await page.goto('http://app.localhost:3000/student/code-redemption');

    // 3. Redeem code using the student-code-activation-input ID
    await page.fill('#student-code-activation-input', generatedCode);
    await page.click('button:has-text("تفعيل الكود")');

    // Confirm code activation on confirmation screen
    await page.click('button:has-text("تأكيد التفعيل")');

    // 4. Verify balance updated (success container is visible)
    await expect(page.locator('#student-code-activation-success')).toBeVisible({ timeout: 10000 });

    // 5. Direct Purchase Check
    await page.goto('http://app.localhost:3000/student/packages');
    
    // Click "استعرض الباقة" for the mock package
    await page.locator('text=استعرض الباقة').first().click();
    await expect(page).toHaveURL(/.*\/packages\/.*/);

    // Click "شراء الباقة" (Buy Package)
    await page.click('button:has-text("شراء الباقة")');

    // Click "تأكيد الخصم والشراء" inside the purchase modal
    await page.click('button:has-text("تأكيد الخصم والشراء")');

    // Verify successful purchase
    await expect(page.locator('text=تم الشراء بنجاح!')).toBeVisible({ timeout: 10000 });
  });
});

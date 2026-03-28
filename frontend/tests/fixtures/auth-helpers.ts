import { expect, Page } from '@playwright/test';

export interface StudentTestContext {
  phone: string;
  nationalId: string;
  randomSuffix: string;
}

/**
 * Generates deterministic structured student data that won't collide.
 */
export function generateStudentContext(): StudentTestContext {
  const timestamp = Date.now().toString().slice(-6);
  // Phone starts with 015, followed by random timestamp + random 2 digits to ensure unique
  const randomSuffix = Math.floor(Math.random() * 90) + 10;
  const phone = `015${timestamp}${randomSuffix}`;

  // valid Egyptian national ID format (14 digits)
  // e.g. 3 (2000+) + 05 (year) + 01 (mon) + 01 (day) + 01 (gov) + 1234 (random) + 5
  const nationalId = `3${timestamp}010112345`.slice(0, 14);

  return {
    phone,
    nationalId,
    randomSuffix: randomSuffix.toString(),
  };
}

/**
 * Automates the auth bypass flow or regular login for E2E tests,
 * assuming the OTP is disabled in the E2E backend or a bypass code (e.g. 123456) is active.
 */
export async function authenticateE2E(
  page: Page,
  phone: string,
  isNewStudent = false,
  password = 'TestPass123!'
) {
  await page.goto('/login');

  // Fill in credentials
  await page.fill('input[name="phoneNumber"]', phone);
  // Default to the common test password or one passed in
  await page.fill('input[name="password"]', password);
  // Force click to bypass any animation overlays like feature-carousel intercepting pointer events
  await page.click('button[type="submit"]', { force: true });

  // Verify successful redirect
  if (isNewStudent) {
    await expect(page).toHaveURL(/.*\/onboarding/);
  } else {
    // Both standard dash and profile completeness checks land here initially or direct to dashboard
    await expect(page).toHaveURL(/.*\/student|.*\/dashboard/);
  }
}

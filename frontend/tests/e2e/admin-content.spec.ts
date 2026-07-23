import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

const adminBaseUrl = process.env.E2E_ADMIN_URL ?? 'http://localhost:8740';
const teacherBaseUrl = process.env.E2E_TEACHER_URL ?? 'http://localhost:8741';

async function setupMockPackage(request: APIRequestContext): Promise<string> {
  const response = await request.post('http://localhost:5245/api/e2e/setup-mock-package');
  expect(response.ok()).toBeTruthy();
  const setup = await response.json();
  return setup.lessonId;
}

async function login(page: Page, phoneNumber = '20000000000') {
  const surfaceBaseUrl = phoneNumber === '20000000000' ? adminBaseUrl : teacherBaseUrl;
  await page.goto(`${surfaceBaseUrl}/login`);
  await page.fill('input[name="phoneNumber"]', phoneNumber);
  await page.fill('input[name="password"]', 'password');
  if (phoneNumber === '20000000000') {
    await page.click('text=تذكرني', { force: true });
  }
  await page.click('button[type="submit"]', { force: true });
  if (phoneNumber === '20000000000') {
    await expect(page).toHaveURL(/.*\/admin$/, { timeout: 15000 });
  } else {
    await expect(page).toHaveURL(/.*\/teacher$/, { timeout: 15000 });
  }
}

test.describe('Admin Content Management Flow', () => {
  test('T009 & T010: Create Package, Section, Lesson, and Video', async ({
    request,
    page,
  }) => {
    test.setTimeout(60000);
    await setupMockPackage(request);
    await login(page);
    // Navigate to content management
    await page.goto(`${adminBaseUrl}/admin/content`);
    await expect(page.getByText('إدارة المحتوى', { exact: true }).first()).toBeVisible();

    // Click on the teacher card
    await page.click('text=E2E Teacher');
    await expect(page.locator('text=إضافة باقة جديدة')).toBeVisible({ timeout: 10000 });

    // Click the "إضافة باقة جديدة" button
    await page.click('text=إضافة باقة جديدة');

    // Fill the modal form (uses placeholder-based inputs)
    const uniqueName = `E2E Pkg ${Date.now()}`;
    await page.fill('input[placeholder*="اسم الباقة"]', uniqueName);
    await page.fill('textarea[placeholder*="وصف مختصر"]', 'E2E test package');
    await page.fill('input[placeholder*="السعر"]', '100');

    // Select Subject and Grade Level (Teacher is already selected by context)
    await page.getByRole('combobox', { name: 'اختر المادة...' }).click();
    await page.getByRole('option').first().click();

    await page.getByRole('combobox', { name: 'اختر الصف الدراسي...' }).click();
    await page.getByRole('option').first().click();

    // Submit
    await page.click('button:has-text("حفظ الباقة")', { force: true });

    // Wait for package to appear
    await expect(page.locator(`text=${uniqueName}`)).toBeVisible({
      timeout: 10000,
    });

    const packageCard = page.locator('div.rounded-2xl').filter({ hasText: uniqueName }).first();
    await packageCard.locator('a[title="عرض تفاصيل الباقة"]').click({ force: true });

    // Wait for the details page to load (specifically wait for the tab "نظرة عامة" to be visible)
    await expect(page.locator('text=نظرة عامة')).toBeVisible({ timeout: 15000 });

    // Default tab is Terms
    // Click "إضافة" to create a Term
    await page.click('button:has-text("إضافة")', { force: true });
    await page.fill('input[placeholder*="اسم الترم"]', 'E2E Term');
    await page.locator('button:has-text("حفظ"):visible').first().dispatchEvent('click');

    // Wait for the term to appear and click it to go to Term details
    await expect(page.locator('text=E2E Term')).toBeVisible({ timeout: 10000 });
    await page.click('text=E2E Term', { force: true });

    // Wait for Term details page to load
    await expect(page.locator('button:has-text("الشهور / الأقسام")')).toBeVisible({ timeout: 15000 });

    // 3. We are on Term details. Go to sections tab
    await page.click('button:has-text("الشهور / الأقسام")', { force: true });
    await page.click('button:has-text("إضافة")', { force: true });
    await page.fill('input[placeholder*="اسم القسم"]', 'E2E Section');
    await page.locator('button:has-text("حفظ"):visible').first().dispatchEvent('click');

    // Wait for section and click it to go to Section details
    await expect(page.locator('text=E2E Section')).toBeVisible({ timeout: 10000 });
    await page.click('text=E2E Section', { force: true });

    // Wait for Section details page to load
    await expect(page.locator('button:has-text("الحصص")')).toBeVisible({ timeout: 15000 });

    // 4. We are on Section details. Go to lessons tab
    await page.click('button:has-text("الحصص")', { force: true });
    await page.click('button:has-text("إضافة")', { force: true });
    await page.fill('input[placeholder*="عنوان الحصة"]', 'E2E Lesson');
    await page.fill('textarea[placeholder*="نبذة قصيرة"]', 'E2E lesson summary');
    await page.locator('button:has-text("حفظ"):visible').first().dispatchEvent('click');

    // Wait for lesson to appear
    await expect(page.locator('text=E2E Lesson')).toBeVisible({ timeout: 10000 });
  });

  test('admin manages video types and preserves content codes', async ({ request, page }) => {
    const seededLessonId = await setupMockPackage(request);
    await login(page);
    await page.goto(`${adminBaseUrl}/admin/content/video-types`);
    await expect(page.getByRole('heading', { name: 'أنواع الفيديو' })).toBeVisible();
    await expect(page.getByText('شرح', { exact: true })).toBeVisible();

    const uniqueType = `E2E Type ${Date.now()}`;
    await page.getByLabel('اسم النوع').fill(uniqueType);
    await page.getByLabel('ترتيب العرض').fill('80');
    await page.getByRole('button', { name: 'إضافة النوع' }).click();
    await expect(page.getByRole('row').filter({ hasText: uniqueType })).toBeVisible();

    await page.goto(`${adminBaseUrl}/admin/content`);
    await expect(page.getByRole('link', { name: 'إدارة أنواع الفيديو' })).toBeVisible();

    await page.goto(`${adminBaseUrl}/admin/content/lessons/${seededLessonId}`);
    await expect(page.getByText(/^LES-[0-9a-f]{32}$/)).toBeVisible();
    await page.getByRole('button', { name: 'الفيديوهات', exact: true }).click();
    await expect(page.getByText(/^VID-[0-9a-f]{32}$/)).toBeVisible();
    const title = `Typed Video ${Date.now()}`;
    await page.getByPlaceholder('مثال: الدرس الأول - مراجعة').fill(title);
    await page.getByPlaceholder('رابط الفيديو').fill('https://youtu.be/dQw4w9WgXcQ');
    const addVideo = page.getByRole('button', { name: 'إضافة الفيديو' });
    await expect(addVideo).toBeDisabled();
    await page.getByRole('combobox', { name: 'نوع الفيديو' }).click();
    await page.getByRole('option', { name: uniqueType }).click();
    await expect(addVideo).toBeEnabled();
    await addVideo.click();
    let videoRow = page.locator('div.rounded-xl').filter({ hasText: title }).first();
    await expect(videoRow).toContainText(uniqueType);
    await expect(videoRow.getByText(/^VID-[0-9a-f]{32}$/)).toBeVisible();
    const originalCode = await videoRow.getByText(/^VID-[0-9a-f]{32}$/).textContent();
    expect(originalCode).toMatch(/^VID-[0-9a-f]{32}$/);

    await page.goto(`${adminBaseUrl}/admin/content/video-types`);
    const row = page.getByRole('row').filter({ hasText: uniqueType });
    await row.getByRole('button', { name: `تعطيل ${uniqueType}` }).click();
    await expect(row).toContainText('معطل');
    await row.getByRole('button', { name: `حذف ${uniqueType}` }).click();
    await page.getByRole('button', { name: 'حذف النوع' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'النوع مستخدم في فيديوهات حالية. عطّله بدلاً من حذفه.' }).first()).toBeVisible();
    await expect(row).toBeVisible();

    await page.goto(`${adminBaseUrl}/admin/content/lessons/${seededLessonId}`);
    await page.getByRole('button', { name: 'الفيديوهات', exact: true }).click();
    videoRow = page.locator('div.rounded-xl').filter({ hasText: title }).first();
    await videoRow.getByRole('button', { name: 'تعديل الفيديو' }).click();
    await expect(videoRow.getByRole('combobox', { name: 'نوع الفيديو' })).toContainText(`${uniqueType} (معطل، مستخدم حالياً)`);
    await videoRow.getByPlaceholder('مثال: الدرس الأول - مراجعة').fill(`${title} Updated`);
    await videoRow.getByRole('button', { name: 'حفظ التعديلات' }).click();
    videoRow = page.locator('div.rounded-xl').filter({ hasText: `${title} Updated` }).first();
    await expect(videoRow.getByText(originalCode!)).toBeVisible();

  });

  test('teacher cannot open video type management', async ({ page }) => {
    await login(page, '20000000004');
    await page.goto(`${adminBaseUrl}/admin/content/video-types`);
    await expect(page).not.toHaveURL(/\/admin\/content\/video-types$/);
  });
});

import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdir } from 'node:fs/promises';
import { chromium, expect } from '@playwright/test';

const origin = 'http://app.lvh.me:8738';
const student = { id: 'student', fullName: 'طالب التجربة', roles: ['Student'], permissions: [],
  allowedDomains: ['student'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };

for (const width of [390, 1440]) {
  test(`teacher recharge opens suggestions and purchases only the selected item (${width}px)`, { timeout: 120000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage({ viewport: { width, height: 950 } });
      await page.addInitScript(user => {
        localStorage.setItem('accessToken', 'synthetic-token');
        localStorage.setItem('user', JSON.stringify(user));
      }, student);
      const purchases = [];
      let rejectPurchase = true;
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
        let data = [];
        if (path === '/auth/session') data = { user: student, authorizationVersion: 1 };
        if (path === '/student/welcome/claim') data = null;
        if (path === '/public/settings') return route.fulfill({ json: { maintenanceMode: false } });
        if (path === '/student/shell-bootstrap') data = { currentBalance: 0, unreadNotificationsCount: 0,
          hasSeenTrackingCodePopup: true, gamification: { totalPoints: 0, levelName: 'طالب' } };
        if (path === '/student/balance') data = { currentBalance: 0, recentTransactions: [], promotionalBalance: 100, promotionalAllocations: [] };
        if (path.startsWith('/codes/validate/')) data = { code: 'TESTCREDIT100', codeType: 'Balance', teacherId: 'teacher',
          targetName: 'رصيد خاص بقيمة ١٠٠ جنيه', teacherName: 'إسلام صبرة', teacherProfileImageUrl: '/test-avatar.svg' };
        if (path === '/codes/activate') data = { grantId: 'redeemed', grantType: 'Balance', message: 'تم الشحن', redirectUrl: '/student/balance/recommendations/redeemed' };
        if (path === '/codes/recharge-recommendations/redeemed') data = { teacherId: 'teacher', teacherName: 'إسلام صبرة',
          rechargedAmount: 100, availableBalance: 150, page: 1, hasMore: false, items: [
            { contentType: 'Lesson', contentId: 'lesson-new', title: 'الحصة الأحدث', price: 100, packageName: 'الشهر الثالث', openUrl: '/student/packages/course/lessons/lesson-new' },
            { contentType: 'Lesson', contentId: 'lesson-old', title: 'الحصة الأقدم', price: 100, packageName: 'الشهر الثاني', openUrl: '/student/packages/course/lessons/lesson-old' },
            { contentType: 'Month', contentId: 'month', title: 'شهر بسعر أقل', price: 75, packageName: 'كورس التاريخ', openUrl: '/student/packages/course/terms/term/sections/month' },
          ] };
        if (path === '/student/balance/purchase-preview') data = { price: 100, discountedPrice: 100, currentPaidBalance: 50,
          eligiblePromotionalAmount: 100, promotionalAmountToUse: 100, paidAmountToUse: 0, isSufficient: true };
        if (path === '/student/balance/purchase') {
          purchases.push(route.request().postDataJSON());
          if (rejectPurchase) return route.fulfill({ json: { success: false, message: 'تعذر الشراء الآن', data: false } });
          data = true;
        }
        return route.fulfill({ json: { success: true, data } });
      });
      await page.goto(`${origin}/student/balance`);
      await page.getByLabel('كود التفعيل', { exact: true }).fill('TESTCREDIT100');
      await page.getByRole('button', { name: 'تفعيل الكود', exact: true }).click();
      await page.getByRole('button', { name: 'تأكيد التفعيل', exact: true }).click();
      await page.waitForURL('**/student/balance/recommendations/redeemed');
      await expect(page.getByRole('heading', { name: 'محتوى إسلام صبرة', exact: true })).toBeVisible();
      assert.deepEqual(await page.locator('article h2').allTextContents(), ['الحصة الأحدث', 'الحصة الأقدم', 'شهر بسعر أقل']);
      assert.equal(purchases.length, 0);
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      await mkdir('../artifacts/teacher-balance-discovery-20261124', { recursive: true });
      await page.screenshot({ path: `../artifacts/teacher-balance-discovery-20261124/student-${width}.png`, fullPage: true });
      await page.getByRole('button', { name: 'شراء وفتح الحصة الأحدث', exact: true }).click();
      const dialog = page.getByRole('dialog');
      const confirm = dialog.getByRole('button', { name: 'تأكيد الخصم والشراء', exact: true });
      await confirm.click();
      await expect(dialog.getByText('فشل في عملية الشراء', { exact: true })).toBeVisible();
      assert.ok(page.url().endsWith('/student/balance/recommendations/redeemed'));
      rejectPurchase = false;
      await confirm.click();
      await page.waitForURL('**/student/packages/course/lessons/lesson-new');
      assert.equal(purchases.length, 2);
      assert.equal(purchases[1].contentId, 'lesson-new');
      assert.equal(purchases[1].contentType, 'Lesson');
      assert.equal(purchases[1].teacherId, undefined);
    } finally { await browser.close(); }
  });
}

test('code creation defaults to teacher balance and requires an explicit platform choice', { timeout: 120000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
    const admin = { ...student, id: 'admin', roles: ['Admin'], allowedDomains: ['admin'], permissions: ['codes.manage'] };
    await page.addInitScript(user => {
      localStorage.setItem('accessToken', 'synthetic-token');
      localStorage.setItem('user', JSON.stringify(user));
    }, admin);
    const generated = [];
    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
      let data = [];
      if (path === '/auth/session') data = { user: admin, authorizationVersion: 1 };
      if (path === '/admin/teachers') data = [{ id: 'teacher', fullName: 'إسلام صبرة' }];
      if (path === '/admin/codes/bulk-generate') {
        generated.push(route.request().postDataJSON());
        data = { codeGroupId: 'group', codesGenerated: 10, codes: [] };
      }
      return route.fulfill({ json: { success: true, data } });
    });
    await page.goto('http://admin.lvh.me:8738/admin/codes');
    await page.getByRole('button', { name: 'إنشاء دفعة جديدة', exact: true }).click();
    await expect(page.getByRole('button', { name: /رصيد خاص بمدرس/ })).toHaveAttribute('aria-pressed', 'true');
    const teacherSelect = page.getByLabel('المدرس صاحب الرصيد (مطلوب)', { exact: true });
    await expect(teacherSelect).toHaveAttribute('required', '');
    await page.getByPlaceholder('50', { exact: true }).fill('100');
    await page.getByRole('button', { name: 'توليد الدفعة', exact: true }).click();
    assert.equal(generated.length, 0);
    await teacherSelect.selectOption('teacher');
    await page.getByRole('button', { name: 'توليد الدفعة', exact: true }).click();
    await expect(page.getByText('تم التوليد بنجاح!', { exact: true })).toBeVisible();
    assert.equal(generated[0].teacherId, 'teacher');
    assert.equal(generated[0].balanceScope, 'Teacher');
    await page.getByRole('button', { name: 'إنشاء دفعة جديدة', exact: true }).click();
    await page.getByRole('button', { name: /رصيد عام للمنصة/ }).click();
    await page.getByPlaceholder('50', { exact: true }).fill('100');
    await page.getByRole('button', { name: 'توليد الدفعة', exact: true }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    assert.equal(generated[1].balanceScope, 'Platform');
    assert.equal(generated[1].teacherId, undefined);
  } finally { await browser.close(); }
});

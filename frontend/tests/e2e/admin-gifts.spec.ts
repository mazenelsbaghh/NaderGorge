import { expect, test, type Page, type Route } from '@playwright/test';

const adminBaseUrl = process.env.E2E_ADMIN_URL ?? 'http://localhost:8740';

async function login(page: Page) {
  await page.addInitScript(() => {
    localStorage.setItem('accessToken', 'e2e-admin-token');
    localStorage.setItem('user', JSON.stringify({
      id: 'admin-152',
      fullName: 'Admin',
      phone: '20000000000',
      roles: ['Admin'],
      permissions: [],
      profileComplete: true,
      allowedDomains: ['admin'],
      allowedNavbarItems: [],
    }));
  });
  await page.route('**/api/admin/wallets**', (route) => fulfill(route, []));
}

const ok = (data: unknown) => ({ success: true, data });
const fulfill = (route: Route, data: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(ok(data)) });

test.describe('Admin gifts workspace', () => {
  test('shows ledger, shell navigation, details, and audited revoke dialog', async ({ page }) => {
    const giftId = '11111111-1111-1111-1111-111111111111';
    await page.route('**/api/admin/gifts', (route) => {
      if (route.request().method() !== 'GET') return route.continue();
      return fulfill(route, {
        items: [{ id: giftId, targetType: 'Video', targetName: 'شرح الوحدة الأولى', status: 'Active', issuerName: 'Admin', recipientCount: 2, successfulCount: 1, originalValue: null, availableValue: null, expiresAt: null, issuedAt: '2026-06-29T12:00:00Z' }],
        page: 1, pageSize: 20, totalCount: 1, totalPages: 1,
      });
    });
    await page.route(`**/api/admin/gifts/${giftId}`, (route) => fulfill(route, {
      id: giftId, requestId: crypto.randomUUID(), targetType: 'Video', targetName: 'شرح الوحدة الأولى', status: 'Active', issuerName: 'Admin', reason: 'تعويض مشكلة تشغيل', amount: null, availableAmount: 0, consumedAmount: 0, expiredAmount: 0, revokedAmount: 0, expiresAt: null, maxUses: 2, issuedAt: '2026-06-29T12:00:00Z',
      recipients: [{ studentId: 'student-1', studentName: 'طالب تجريبي', status: 'PartiallyUsed', outcomeCode: 'GRANTED', outcomeMessage: null, usesConsumed: 1, maxUses: 2 }],
    }));

    await login(page);
    await page.goto(`${adminBaseUrl}/admin/gifts`);
    await expect(page.getByRole('heading', { name: 'الهدايا والوصول المجاني' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'الهدايا' }).first()).toBeVisible();
    await expect(page.getByText('شرح الوحدة الأولى').first()).toBeVisible();
    await page.getByTitle('فتح التفاصيل').click();
    await expect(page.getByText('تعويض مشكلة تشغيل')).toBeVisible();
    await expect(page.getByText('طالب تجريبي')).toBeVisible();
    await page.getByRole('button', { name: 'إلغاء المتبقي' }).click();
    await expect(page.getByRole('dialog', { name: 'إلغاء المتبقي من الهدية' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'تأكيد الإلغاء' })).toBeDisabled();
  });

  test('issues a teacher-restricted promotional balance for selected students', async ({ page }) => {
    await page.route('**/api/admin/gifts/lookups/students**', (route) => fulfill(route, [
      { id: '22222222-2222-2222-2222-222222222222', name: 'أحمد محمد', context: '01000000001' },
      { id: '33333333-3333-3333-3333-333333333333', name: 'سارة علي', context: '01000000002' },
    ]));
    await page.route('**/api/admin/gifts/lookups/teachers**', (route) => fulfill(route, [
      { id: '44444444-4444-4444-4444-444444444444', name: 'مستر نادر', context: 'فيزياء' },
    ]));
    let submitted: Record<string, unknown> | null = null;
    await page.route('**/api/admin/gifts', async (route) => {
      if (route.request().method() !== 'POST') return route.continue();
      submitted = route.request().postDataJSON();
      return fulfill(route, { id: '55555555-5555-5555-5555-555555555555', requestId: submitted?.requestId, targetType: 'TeacherBalance', status: 'Active', targetName: 'رصيد مدرس: مستر نادر', isReplay: false, recipients: [] }, 201);
    });
    await page.route('**/api/admin/gifts/55555555-5555-5555-5555-555555555555', (route) => fulfill(route, {
      id: '55555555-5555-5555-5555-555555555555', requestId: crypto.randomUUID(), targetType: 'TeacherBalance', targetName: 'رصيد مدرس: مستر نادر', status: 'Active', issuerName: 'Admin', reason: 'حملة تفوق', amount: 75, availableAmount: 75, consumedAmount: 0, expiredAmount: 0, revokedAmount: 0, expiresAt: null, maxUses: 2, issuedAt: '2026-06-29T12:00:00Z', recipients: [],
    }));

    await login(page);
    await page.goto(`${adminBaseUrl}/admin/gifts/new`);
    await page.getByRole('button', { name: 'رصيد مخصص لمدرس' }).click();
    await page.getByRole('combobox', { name: 'المدرس' }).selectOption('44444444-4444-4444-4444-444444444444');
    await page.getByLabel('قيمة الرصيد').fill('75');
    await page.getByRole('button', { name: /أحمد محمد/ }).click();
    await page.getByLabel(/عدد المشتريات/).fill('2');
    await page.getByLabel('سبب الهدية').fill('حملة تفوق');
    await page.getByRole('button', { name: 'إصدار الهدية' }).click();

    await expect.poll(() => submitted).not.toBeNull();
    expect(submitted).toMatchObject({ targetType: 'TeacherBalance', teacherId: '44444444-4444-4444-4444-444444444444', amount: 75, maxUses: 2, reason: 'حملة تفوق', studentIds: ['22222222-2222-2222-2222-222222222222'] });
    await expect(page).toHaveURL(/\/admin\/gifts\/55555555-5555-5555-5555-555555555555$/);
  });

  test('lists gift packages directly and clears lesson search when switching packages', async ({ page }) => {
    const ids = {
      teacher: '44444444-4444-4444-4444-444444444444',
      package: '55555555-5555-5555-5555-555555555555',
      term: '66666666-6666-6666-6666-666666666666',
      section: '77777777-7777-7777-7777-777777777777',
      lesson: '88888888-8888-8888-8888-888888888888',
      student: '99999999-9999-9999-9999-999999999999',
      secondPackage: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      secondTerm: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      secondSection: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      secondLesson: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
    };
    await page.route('**/api/admin/gifts/lookups/students**', (route) => fulfill(route, [
      { id: ids.student, name: 'طالب تجريبي', context: '01000000001' },
    ]));
    await page.route('**/api/admin/gifts/lookups/teachers**', (route) => {
      const search = new URL(route.request().url()).searchParams.get('search');
      return fulfill(route, search && !'مستر نادر'.includes(search) ? [] : [
        { id: ids.teacher, name: 'مستر نادر', context: 'فيزياء' },
      ]);
    });
    await page.route('**/api/admin/gifts/lookups/targets**', (route) => {
      const query = new URL(route.request().url()).searchParams;
      const rows: Record<string, { id: string; name: string; isSystemContainer?: boolean }[]> = {
        Package: [{ id: ids.package, name: 'الكورس الأول' }, { id: ids.secondPackage, name: 'الكورس الثاني' }],
      };
      const type = query.get('targetType') ?? '';
      const children: Record<string, { id: string; name: string; isSystemContainer?: boolean }[]> = {
        [`Term:${ids.package}`]: [{ id: ids.term, name: 'محتوى الباقة', isSystemContainer: true }],
        [`Term:${ids.secondPackage}`]: [{ id: ids.secondTerm, name: 'محتوى الباقة', isSystemContainer: true }],
        [`ContentSection:${ids.term}`]: [{ id: ids.section, name: 'حصص مباشرة', isSystemContainer: true }],
        [`ContentSection:${ids.secondTerm}`]: [{ id: ids.secondSection, name: 'حصص مباشرة', isSystemContainer: true }],
        [`Lesson:${ids.section}`]: [{ id: ids.lesson, name: 'الحصة الأولى' }],
        [`Lesson:${ids.secondSection}`]: [{ id: ids.secondLesson, name: 'الحصة الثانية' }],
      };
      const search = query.get('search') ?? '';
      const options = children[`${type}:${query.get('parentId')}`] ?? rows[type] ?? [];
      return fulfill(route, options.filter((row) => row.name.includes(search)));
    });
    let submitted: Record<string, unknown> | null = null;
    await page.route('**/api/admin/gifts', (route) => {
      if (route.request().method() !== 'POST') return route.continue();
      submitted = route.request().postDataJSON();
      return fulfill(route, { id: crypto.randomUUID(), requestId: submitted?.requestId, targetType: 'Lesson', status: 'Active', recipients: [] }, 201);
    });

    await login(page);
    await page.goto(`${adminBaseUrl}/admin/gifts/new`);
    await page.getByRole('button', { name: 'حصة', exact: true }).click();
    await page.getByLabel('ابحث عن المدرس').fill('نادر');
    await page.getByRole('combobox', { name: 'المدرس' }).selectOption(ids.teacher);
    await expect(page.getByLabel('ابحث عن الباقة')).toHaveCount(0);
    await expect(page.getByRole('combobox', { name: 'الباقة' }).getByRole('option')).toHaveText(['اختر الباقة', 'الكورس الأول', 'الكورس الثاني']);
    await page.getByRole('combobox', { name: 'الباقة' }).selectOption(ids.package);
    await page.getByLabel('ابحث عن الحصة').fill('الأولى');
    await page.getByRole('combobox', { name: 'الحصة' }).selectOption(ids.lesson);
    await page.getByRole('combobox', { name: 'الباقة' }).selectOption(ids.secondPackage);
    await expect(page.getByLabel('ابحث عن الحصة')).toHaveValue('');
    await expect(page.getByRole('combobox', { name: 'الحصة' })).toHaveValue('');
    await expect(page.getByRole('combobox', { name: 'الحصة' }).getByRole('option')).toHaveText(['اختر الحصة', 'الحصة الثانية']);
    await page.getByRole('combobox', { name: 'الحصة' }).selectOption(ids.secondLesson);
    await page.getByRole('button', { name: /طالب تجريبي/ }).click();
    await page.getByLabel('سبب الهدية').fill('تعويض عن مشكلة تشغيل');
    await page.getByRole('button', { name: 'إصدار الهدية' }).click();

    await expect.poll(() => submitted).not.toBeNull();
    expect(submitted).toMatchObject({ targetType: 'Lesson', targetId: ids.secondLesson, studentIds: [ids.student], reason: 'تعويض عن مشكلة تشغيل' });
  });
});

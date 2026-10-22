import { expect, test, type Page, type Route } from '@playwright/test';
import type { CodeGroupDto } from '../../src/services/admin-service';

const first: CodeGroupDto = {
  id: '82000000-0000-0000-0000-000000000001', name: 'دفعة أكتوبر', createdAt: '2026-10-01T10:00:00Z',
  codeType: 'Package', packageId: 'package-1', includeFutureVideos: false, expireActivatedAccess: false,
  accountingTiming: 'OnActivation', codeCount: 100, usedCount: 20,
};
const second: CodeGroupDto = { ...first, id: '82000000-0000-0000-0000-000000000002', name: 'دفعة سبتمبر', packageId: undefined };
const reply = (route: Route, data: unknown) => route.fulfill({ json: { success: true, data } });

async function teacherCodes(page: Page, baseURL: string, search: (route: Route, query: string) => Promise<void>) {
  const user = { id: '82000000-0000-0000-0000-000000000099', fullName: 'مدرس الاختبار', roles: ['Teacher'],
    permissions: [], profileComplete: true, allowedDomains: ['teacher'], allowedNavbarItems: [], authorizationVersion: 1 };
  await page.addInitScript(authUser => {
    localStorage.setItem('accessToken', 'synthetic-teacher-token');
    localStorage.setItem('user', JSON.stringify(authUser));
  }, user);
  await page.route('**/api/**', route => reply(route, []));
  await page.route('**/api/auth/session', route => reply(route, { user, authorizationVersion: 1 }));
  await page.route('**/api/public/settings', route => route.fulfill({ json: { maintenanceMode: false } }));
  await page.route('**/api/teacher/context', route => reply(route, { isOwner: true, permissionKeys: ['codes', 'content'] }));
  await page.route('**/api/content/packages*', route => reply(route, [{ id: 'package-1', name: 'باقة التاريخ' }]));
  const detailRequests: string[] = [];
  const adminRequests: string[] = [];
  page.on('request', request => {
    if (request.url().includes('/codes/groups/') && request.url().includes('/details')) detailRequests.push(request.url());
    if (request.url().includes('/api/admin/codes')) adminRequests.push(request.url());
  });
  await page.route('**/api/teacher/codes/groups*', route => {
    const query = new URL(route.request().url()).searchParams.get('search');
    return query ? search(route, query) : reply(route, [first, second]);
  });
  const url = new URL(baseURL);
  url.hostname = 'teacher.lvh.me'; url.pathname = '/teacher/codes';
  await page.goto(url.toString());
  await expect(page.getByText(first.name, { exact: true })).toBeVisible();
  await expect(page.getByText(second.name, { exact: true })).toBeVisible();
  return { detailRequests, adminRequests, input: page.getByRole('textbox', { name: 'البحث', exact: true }) };
}

// 2026-10-22: teachers could search codes only after opening a code group.
for (const query of ['784501', 'HISTORY-ACCESS-42']) {
  test(`teacher finds the matching group by ${query} from the main page without opening group files`, async ({ page, baseURL }) => {
    const setup = await teacherCodes(page, baseURL!, (route, value) => reply(route, value === query ? [first] : []));
    await setup.input.fill(query);
    await expect(page.getByText(second.name, { exact: true })).toHaveCount(0);
    await expect(page.getByText(first.name, { exact: true })).toBeVisible();
    await expect(page.locator(`a[href="/teacher/codes/${first.id}"]`)).toBeVisible();
    expect(setup.detailRequests).toEqual([]);
    expect(setup.adminRequests).toEqual([]);
    await setup.input.fill('باقة التاريخ');
    await expect(page.getByText(first.name, { exact: true })).toBeVisible();
    await expect(page.getByText(second.name, { exact: true })).toHaveCount(0);
    await setup.input.clear();
    await expect(page.getByText(second.name, { exact: true })).toBeVisible();
  });
}

test('a failed code lookup offers retry instead of claiming no matches', async ({ page, baseURL }) => {
  let unavailable = true;
  const { input } = await teacherCodes(page, baseURL!, route => unavailable
    ? route.fulfill({ status: 503, json: { success: false, message: 'البحث غير متاح مؤقتًا' } }) : reply(route, [second]));
  await input.fill('991122');
  const alert = page.getByRole('alert').filter({ hasText: 'تعذر البحث بالكود أو السيريال' });
  await expect(alert).toBeVisible();
  await expect(page.getByText(/لا توجد مجموعات تطابق البحث/)).toHaveCount(0);
  unavailable = false;
  await alert.getByRole('button', { name: 'إعادة المحاولة', exact: true }).click();
  await expect(page.getByText(second.name, { exact: true })).toBeVisible();
  await expect(alert).toHaveCount(0);
  await expect(page.getByText(first.name, { exact: true })).toHaveCount(0);
});

test('a delayed earlier lookup cannot replace the latest matching group', async ({ page, baseURL }) => {
  let releaseOld!: () => void;
  const oldReady = new Promise<void>(resolve => { releaseOld = resolve; });
  let oldRequested = false;
  const { input } = await teacherCodes(page, baseURL!, async (route, query) => {
    if (query === '111111') { oldRequested = true; await oldReady; return reply(route, [first]); }
    return reply(route, [second]);
  });
  try {
    await input.fill('111111');
    await expect.poll(() => oldRequested).toBe(true);
    await input.fill('222222');
    await expect(page.getByText(second.name, { exact: true })).toBeVisible();
    const oldResponse = page.waitForResponse(response => response.url().includes('search=111111'));
    releaseOld();
    await (await oldResponse).finished();
    await expect(page.getByRole('status').filter({ hasText: 'جارٍ البحث في أكوادك' })).toHaveCount(0);
    await expect(page.getByText(second.name, { exact: true })).toBeVisible();
    await expect(page.getByText(first.name, { exact: true })).toHaveCount(0);
  } finally { releaseOld(); }
});

import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium, expect } from '@playwright/test';

const modes = ['TermWithSections', 'SectionWithLessons', 'LessonsOnly', 'SingleLesson'];
const packages = modes.map((contentMode) => ({
  id: contentMode, name: `كورس ${contentMode}`, contentMode, description: '', price: 100,
  programId: 'subject', isEnrolled: false,
  rootTermId: contentMode === 'TermWithSections' ? undefined : `${contentMode}-term`,
  rootSectionId: ['LessonsOnly', 'SingleLesson'].includes(contentMode) ? `${contentMode}-section` : undefined,
}));

async function choose(page, label, option) {
  await page.getByRole('combobox', { name: label, exact: true }).click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

async function openCodes(page) {
  const user = { id: 'test-admin', fullName: 'أدمن الاختبار', roles: ['Admin'], permissions: ['codes.manage'],
    allowedDomains: ['admin'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1 };
  await page.addInitScript((user) => {
    localStorage.setItem('accessToken', 'synthetic-test-token');
    localStorage.setItem('user', JSON.stringify(user));
  }, user);
  const generated = [];
  await page.route('**/api/**', async (route) => {
    const path = new URL(route.request().url()).pathname.replace(/^\/api/, '');
    let response = [];
    if (path === '/auth/session') response = { user, authorizationVersion: 1 };
    if (path === '/content/packages') response = packages;
    const termMatch = path.match(/^\/content\/packages\/([^/]+)\/terms$/);
    if (termMatch) response = [{ id: `${termMatch[1]}-term`, title: 'الترم الأول', order: 1 }];
    const sectionMatch = path.match(/^\/content\/terms\/([^/]+)-term\/sections$/);
    if (sectionMatch) response = [{ id: `${sectionMatch[1]}-section`, title: 'الشهر الأول', order: 1 }];
    const lessonMatch = path.match(/^\/content\/sections\/([^/]+)-section\/lessons$/);
    if (lessonMatch) response = [{ id: `${lessonMatch[1]}-lesson`, title: `حصة ${lessonMatch[1]}`, summary: '', order: 1, hasAccess: true }];
    if (path === '/admin/codes/bulk-generate') {
      generated.push(route.request().postDataJSON());
      response = { codeGroupId: 'generated-group', codesGenerated: 10, codes: ['123456789012'] };
    }
    await route.fulfill({ json: { success: true, data: response } });
  });
  await page.goto('http://admin.lvh.me:3000/admin/codes');
  await page.getByRole('button', { name: 'إنشاء دفعة جديدة', exact: true }).click();
  await page.getByRole('button', { name: 'حصة كود لفتح حصة محددة', exact: true }).click();
  return generated;
}

// Regression: monthly/direct courses previously required an invisible term before lessons could be selected.
for (const mode of modes) {
  test(`lesson code follows ${mode} hierarchy (synthetic API)`, { timeout: 90000 }, async () => {
    const browser = await chromium.launch({ channel: 'chrome' });
    try {
      const page = await browser.newPage();
      const generated = await openCodes(page);
      await choose(page, 'اختر الباكدج', `كورس ${mode}`);
      if (mode === 'TermWithSections') await choose(page, 'اختر الترم', 'الترم الأول');
      else await expect(page.getByRole('combobox', { name: 'اختر الترم', exact: true })).toHaveCount(0);
      if (['TermWithSections', 'SectionWithLessons'].includes(mode)) await choose(page, 'اختر الشهر / القسم', 'الشهر الأول');
      else await expect(page.getByRole('combobox', { name: 'اختر الشهر / القسم', exact: true })).toHaveCount(0);
      await choose(page, 'اختر الحصة', `حصة ${mode}`);
      await page.getByRole('button', { name: 'توليد الدفعة', exact: true }).click();
      await expect(page.getByText('تم التوليد بنجاح!', { exact: true })).toBeVisible();
      assert.equal(generated.length, 1);
      assert.equal(generated[0].codeType, 'Lesson');
      assert.equal(generated[0].packageId, mode);
      assert.equal(generated[0].termId, `${mode}-term`);
      assert.equal(generated[0].contentSectionId, `${mode}-section`);
      assert.equal(generated[0].lessonId, `${mode}-lesson`);
    } finally { await browser.close(); }
  });
}

test('switching from a monthly course clears the previous lesson target', { timeout: 90000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const page = await browser.newPage();
    const generated = await openCodes(page);
    await choose(page, 'اختر الباكدج', 'كورس LessonsOnly');
    await choose(page, 'اختر الحصة', 'حصة LessonsOnly');
    await choose(page, 'اختر الباكدج', 'كورس TermWithSections');
    await expect(page.getByRole('combobox', { name: 'اختر الحصة', exact: true })).toBeDisabled();
    await choose(page, 'اختر الترم', 'الترم الأول');
    await choose(page, 'اختر الشهر / القسم', 'الشهر الأول');
    await page.getByRole('combobox', { name: 'اختر الحصة', exact: true }).click();
    await expect(page.getByRole('option', { name: 'حصة LessonsOnly', exact: true })).toHaveCount(0);
    await page.getByRole('option', { name: 'حصة TermWithSections', exact: true }).click();
    await page.getByRole('button', { name: 'توليد الدفعة', exact: true }).click();
    await expect(page.getByText('تم التوليد بنجاح!', { exact: true })).toBeVisible();
    assert.equal(generated[0].lessonId, 'TermWithSections-lesson');
  } finally { await browser.close(); }
});

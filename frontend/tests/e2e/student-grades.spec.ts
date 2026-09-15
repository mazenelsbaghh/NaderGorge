import { expect, test } from '@playwright/test';
import { json, openLesson } from '../fixtures/lesson-playback';

for (const width of [1280, 390]) {
  test(`grades are reachable and readable at ${width}px with pending scores hidden`, async ({ page }) => {
    await page.setViewportSize({ width, height: 850 });
    let fail = false;
    await openLesson(page, async () => {
      await page.route('**/api/student/grades?*', async route => {
        if (fail) return route.fulfill({ status: 503, json: { success: false } });
        const url = new URL(route.request().url());
        const kind = url.searchParams.get('kind');
        const next = url.searchParams.get('page') === '2';
        const items = kind === 'homework' ? [] : next ? [
          { id: 'old', kind: 'exam', title: 'محاولة سابقة', lessonTitle: 'الحركة', status: 'Graded', score: 6, totalScore: 10, attemptedAt: '2026-09-01T10:00:00Z' },
        ] : [
          { id: 'exam', kind: 'exam', title: 'امتحان الحركة', lessonTitle: 'الحصة الأولى', status: 'Graded', score: 18, totalScore: 20, attemptedAt: '2026-09-15T10:00:00Z' },
          { id: 'homework', kind: 'homework', title: 'واجب الحركة', lessonTitle: 'الحصة الأولى', status: 'PendingReview', score: null, totalScore: 10, attemptedAt: '2026-09-14T10:00:00Z' },
        ];
        await json(route, { items, totalCount: kind === 'homework' ? 0 : 21, page: next ? 2 : 1, pageSize: 20 });
      });
    });
    await page.getByRole('button', { name: 'إظهار القوائم', exact: true }).click();
    if (width < 768) await page.getByRole('button', { name: 'القائمة', exact: true }).click();
    await page.getByRole('link', { name: 'درجاتي', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'درجاتي', exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'امتحان الحركة' })).toBeVisible();
    const pending = page.getByRole('listitem').filter({ hasText: 'واجب الحركة' });
    await expect(pending).toContainText('قيد التصحيح');
    await expect(pending.getByLabel('الدرجة')).toHaveCount(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.getByRole('button', { name: 'تحديث النتائج', exact: true }).focus();
    await page.mouse.move(100, 100);
    await page.screenshot({ animations: 'disabled', path: `/tmp/massar-grades-${width}.png`, fullPage: true });
    await page.getByRole('button', { name: 'التالي', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'محاولة سابقة' })).toBeVisible();
    await page.getByRole('button', { name: 'الواجبات', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'لا توجد نتائج هنا حتى الآن' })).toBeVisible();
    fail = true;
    await page.getByRole('button', { name: 'الامتحانات', exact: true }).click();
    await expect(page.getByRole('alert').filter({ hasText: 'تعذر تحميل النتائج' })).toBeVisible();
    fail = false;
    await page.getByRole('button', { name: 'تحديث النتائج' }).click();
    await expect(page.getByRole('heading', { name: 'امتحان الحركة' })).toBeVisible();
  });
}

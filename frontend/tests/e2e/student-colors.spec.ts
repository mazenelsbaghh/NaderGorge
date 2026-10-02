import { expect, test } from '@playwright/test';
import { installAuthAndGoto } from './e2e-contract-helpers';

const student = {
  id: 'student-colors', fullName: 'طالب الألوان', phone: '01000000001',
  roles: ['Student'], permissions: [], profileComplete: true,
  allowedDomains: ['student'], allowedNavbarItems: [], avatarSlug: 'messi',
};

test.describe('Student colors (synthetic HTTP)', () => {
  for (const failure of [false, true]) {
    test(failure ? 'failed saves restore the previous colors and mode' : 'independent light and dark colors survive a reload and preserve the avatar', async ({ page, baseURL }, testInfo) => {
      let preferences = {
        selectedLightPaletteId: 'massar-light', selectedDarkPaletteId: 'massar-dark',
        currentMode: 'light', avatarSlug: 'messi', defaultLightPaletteId: 'massar-light',
        defaultDarkPaletteId: 'massar-dark', availableLightPalettes: [], availableDarkPalettes: [],
      };
      await page.route('**/api/**', route => route.fulfill({ json: { success: true, data: [] } }));
      await page.route('**/api/auth/session', route => route.fulfill({ json: { success: true, data: { user: student } } }));
      await page.route('**/api/public/settings', route => route.fulfill({ json: { maintenanceMode: false } }));
      await page.route('**/api/student/shell-bootstrap', route => route.fulfill({ json: { success: true, data: {
        unreadNotificationsCount: 0, currentBalance: 0, hasSeenTrackingCodePopup: true,
        gamification: { totalPoints: 0, levelName: 'طالب' }, themePreferences: preferences,
      } } }));
      await page.route('**/api/student/profile', route => route.fulfill({ json: { success: true, data: {
        fullName: student.fullName, phoneNumber: student.phone, activeDevicesCount: 0,
      } } }));
      await page.route('**/api/student/theme-preferences', async route => {
        if (route.request().method() === 'PUT') {
          if (failure) {
            await route.fulfill({ status: 500, json: { success: false, message: 'فشل الحفظ' } });
            return;
          }
          const update = route.request().postDataJSON();
          preferences = { ...preferences, selectedLightPaletteId: update.lightPaletteId,
            selectedDarkPaletteId: update.darkPaletteId, currentMode: update.currentMode };
        }
        await route.fulfill({ json: { success: true, data: preferences } });
      });

      await installAuthAndGoto(page, 'student-colors-token', student, `${baseURL}/student/profile`);
      await page.getByRole('button', { name: 'تخصيص مظهر المنصة' }).click();
      const settings = page.getByRole('region', { name: 'ألوان المنصة' });
      await settings.getByRole('button', { name: 'واحة هادئة', exact: true }).click();
      if (failure) {
        await expect(settings.getByRole('status')).toContainText('تعذر حفظ المظهر');
        await expect(settings.getByRole('button', { name: 'مسار نهاري', exact: true })).toHaveAttribute('aria-pressed', 'true');
        await expect(page.locator('html')).toHaveAttribute('data-student-theme-palette', 'massar-light');
        await settings.getByRole('button', { name: 'الوضع الداكن', exact: true }).click();
        await expect(settings.getByRole('status')).toContainText('تعذر حفظ المظهر');
        await expect(settings.getByRole('button', { name: 'الوضع الفاتح', exact: true })).toHaveAttribute('aria-pressed', 'true');
        await expect(page.locator('html')).not.toHaveClass(/dark/);
        return;
      }

      await expect(settings.getByRole('status')).toContainText('تم حفظ مظهر المنصة');
      await expect(page.locator('html')).toHaveAttribute('data-student-theme-palette', 'oasis-light');
      await settings.getByRole('button', { name: 'عنبر دافئ', exact: true }).click();
      await expect(settings.getByRole('status')).toContainText('تم حفظ مظهر المنصة');
      await expect(page.locator('html')).toHaveAttribute('data-student-theme-palette', 'oasis-light');
      await settings.getByRole('button', { name: 'الوضع الداكن', exact: true }).click();
      await expect(settings.getByRole('status')).toContainText('تم حفظ مظهر المنصة');
      await expect(page.locator('html')).toHaveAttribute('data-student-theme-palette', 'ember-dark');
      await expect(page.locator('html')).toHaveClass(/dark/);
      await page.reload();
      await page.getByRole('button', { name: 'تخصيص مظهر المنصة' }).click();
      await expect(settings.getByRole('button', { name: 'واحة هادئة', exact: true })).toHaveAttribute('aria-pressed', 'true');
      await expect(settings.getByRole('button', { name: 'عنبر دافئ', exact: true })).toHaveAttribute('aria-pressed', 'true');
      await expect(page.locator('html')).toHaveAttribute('data-student-theme-palette', 'ember-dark');
      expect(preferences.avatarSlug).toBe('messi');
      await settings.screenshot({ path: testInfo.outputPath('colors-desktop.png') });
      await page.setViewportSize({ width: 390, height: 844 });
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
      await settings.screenshot({ path: testInfo.outputPath('colors-mobile.png') });
    });
  }
});

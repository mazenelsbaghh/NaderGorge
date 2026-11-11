import assert from 'node:assert/strict';
import test from 'node:test';
import { chromium, expect } from '@playwright/test';

const origin = process.env.ASSISTANT_APPEARANCE_TEST_ORIGIN ?? 'http://staff.lvh.me:8771';

test('assistant colors apply immediately and follow the account to a fresh browser context', { timeout: 90_000 }, async () => {
  const browser = await chromium.launch({ channel: 'chrome' });
  const user = {
    id: 'appearance-assistant', fullName: 'مساعد تجريبي', roles: ['Assistant'], permissions: [],
    allowedDomains: ['assistant'], allowedNavbarItems: [], profileComplete: true, authorizationVersion: 1,
  };
  let preferences = { currentMode: null, selectedLightPaletteId: 'massar-light', selectedDarkPaletteId: 'massar-dark' };
  let rejectSave = false;

  async function openAppearance(width) {
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    await page.addInitScript(account => {
      localStorage.setItem('accessToken', 'synthetic-assistant-token');
      localStorage.setItem('user', JSON.stringify(account));
    }, user);
    await page.route('**/api/**', async route => {
      const pathname = new URL(route.request().url()).pathname;
      if (pathname.endsWith('/auth/session')) return route.fulfill({ json: { success: true, data: { user, authorizationVersion: 1 } } });
      if (pathname.endsWith('/assistant/theme-preferences')) {
        if (route.request().method() === 'PUT') {
          if (rejectSave) return route.fulfill({ status: 503, json: { success: false } });
          const { currentMode, lightPaletteId, darkPaletteId } = route.request().postDataJSON();
          preferences = { currentMode, selectedLightPaletteId: lightPaletteId, selectedDarkPaletteId: darkPaletteId };
        }
        return route.fulfill({ json: { success: true, data: preferences } });
      }
      return route.fulfill({ json: { success: true, data: [] } });
    });
    await page.goto(`${origin}/assistant/dashboard`);
    const trigger = page.getByRole('button', { name: 'ألوان المنصة', exact: true });
    await trigger.click();
    const dialog = page.getByRole('dialog', { name: 'تخصيص ألوان المنصة' });
    await expect(dialog.getByRole('button', { name: 'واحة هادئة', exact: true })).toBeEnabled();
    return { page, dialog, trigger };
  }

  try {
    const { page, dialog, trigger } = await openAppearance(1280);
    const shell = page.getByTestId('assistant-shell');
    const saved = () => expect(dialog.getByRole('status')).toContainText('تم حفظ مظهر المنصة');
    await dialog.getByRole('button', { name: 'واحة هادئة', exact: true }).click();
    await saved();
    await expect(shell).toHaveCSS('--admin-primary', '#1e6d5f');
    await dialog.getByRole('button', { name: 'عنبر دافئ', exact: true }).click();
    await saved();
    await expect(shell).toHaveCSS('--admin-primary', '#1e6d5f');
    await dialog.getByRole('button', { name: 'الوضع الداكن', exact: true }).click();
    await saved();
    await expect(shell).toHaveCSS('--admin-primary', '#e59a5d');
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(trigger).toBeFocused();
    await page.close();

    const mobile = await openAppearance(390);
    await expect(mobile.dialog.getByRole('button', { name: 'عنبر دافئ', exact: true })).toHaveAttribute('aria-pressed', 'true');
    await expect(mobile.page.getByTestId('assistant-shell')).toHaveCSS('--admin-primary', '#e59a5d');
    rejectSave = true;
    await mobile.dialog.getByRole('button', { name: 'الوضع الفاتح', exact: true }).click();
    await expect(mobile.dialog.getByRole('status')).toContainText('تعذر حفظ المظهر');
    await expect(mobile.page.getByTestId('assistant-shell')).toHaveCSS('--admin-primary', '#e59a5d');
    assert.equal(await mobile.page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
  } finally {
    await browser.close();
  }
});

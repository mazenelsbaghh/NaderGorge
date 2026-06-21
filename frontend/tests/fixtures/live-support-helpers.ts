import { test as base, type BrowserContext, type Page } from '@playwright/test';

type LiveSupportFixtures = {
  guestContext: BrowserContext;
  studentContext: BrowserContext;
  supportAContext: BrowserContext;
  supportBContext: BrowserContext;
  adminContext: BrowserContext;
};

const state = (role: string) => process.env[`LIVE_SUPPORT_${role}_STORAGE_STATE`];
export const test = base.extend<LiveSupportFixtures>({
  guestContext: async ({ browser }, provide) => { const context = await browser.newContext(); await provide(context); await context.close(); },
  studentContext: async ({ browser }, provide) => { const context = await browser.newContext(state('STUDENT') ? { storageState: state('STUDENT') } : {}); await provide(context); await context.close(); },
  supportAContext: async ({ browser }, provide) => { const context = await browser.newContext(state('SUPPORT_A') ? { storageState: state('SUPPORT_A') } : {}); await provide(context); await context.close(); },
  supportBContext: async ({ browser }, provide) => { const context = await browser.newContext(state('SUPPORT_B') ? { storageState: state('SUPPORT_B') } : {}); await provide(context); await context.close(); },
  adminContext: async ({ browser }, provide) => { const context = await browser.newContext(state('ADMIN') ? { storageState: state('ADMIN') } : {}); await provide(context); await context.close(); },
});

export class LiveSupportWidgetPage {
  constructor(readonly page: Page) {}
  async open() { await this.page.getByRole('button', { name: 'فتح الدعم المباشر' }).click(); }
  async startGuest(name: string, phone: string) { await this.page.getByLabel('الاسم').fill(name); await this.page.getByLabel('رقم الهاتف').fill(phone); await this.page.getByRole('button', { name: 'متابعة' }).click(); }
}

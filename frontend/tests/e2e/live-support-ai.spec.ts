import { expect, test, type Page } from '@playwright/test';

const appUrl = process.env.LIVE_SUPPORT_E2E_URL || 'http://localhost:8899';
const conversationId = '14600000-0000-0000-0000-000000000001';

async function mockParticipant(page: Page, turnState: 'Queued' | 'Completed', messages: unknown[]) {
  const conversation = { id: conversationId, status: 'Waiting', participantType: 'Guest', subject: 'AI test', createdAt: new Date().toISOString(), version: 1, canSend: true, canRate: false, isAiActive: true, isAiTyping: turnState === 'Queued' };
  await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 0, code: 'AI_AVAILABLE', message: 'متاح' } }) }));
  await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [conversation] }) }));
  await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/snapshot`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { conversationId, status: 'Waiting', aiMode: 'AiActive', lastSequence: messages.length, canSend: true, aiTurnState: turnState, messages, pendingDecision: null, verification: null } }) }));
}

test.describe('AI live support participant', () => {
  test('AI disclosure and thinking state are explicit at 320px', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await mockParticipant(page, 'Queued', [{ id: crypto.randomUUID(), conversationId, senderType: 'Guest', clientMessageId: 'guest-1', type: 'Text', content: 'محتاج مساعدة', sentAt: new Date().toISOString() }]);
    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();
    await expect(page.getByText('أنت تتحدث الآن مع مساعد ذكي')).toBeVisible();
    await expect(page.getByText('بنراجع رسالتك ونجهّز الرد…')).toBeVisible();
    await expect(page.getByRole('button', { name: /موظف/ })).toBeVisible();
    await expect(page.locator('[role="dialog"]')).toHaveCSS('width', '288px');
  });

  test('AI reply and reconnect snapshot render once without guest data disclosure', async ({ page }) => {
    const aiReply = { id: crypto.randomUUID(), conversationId, senderType: 'AI', clientMessageId: 'ai-turn-1', type: 'Text', content: 'أهلاً، أقدر أساعدك في استفسارك.', sentAt: new Date().toISOString() };
    await mockParticipant(page, 'Completed', [aiReply]);
    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();
    await expect(page.getByRole('log').getByText(aiReply.content)).toHaveCount(1);
    await expect(page.getByRole('log')).not.toContainText(/password|token|010\d{8}/i);
    await page.reload();
    await page.waitForTimeout(300);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();
    await expect(page.getByRole('log').getByText(aiReply.content)).toHaveCount(1);
  });
});

test.describe('AI live support staff and administration', () => {
  test.skip('staff handoff and admin AI journeys continue in their dependent stories', async () => {});
});

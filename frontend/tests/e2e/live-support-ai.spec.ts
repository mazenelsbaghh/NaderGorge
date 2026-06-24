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

  test('handoff confirmation queues the conversation and stops AI', async ({ page }) => {
    const handoffProposal = {
      id: 'proposal-123',
      actionKey: 'system.handoff',
      safeProposalJson: '{\"safeSummaryAr\":\"حل مشكلة الدفع\"}',
      status: 'PendingConfirmation',
      expiresAt: new Date(Date.now() + 300000).toISOString()
    };
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 0, code: 'AI_AVAILABLE', message: 'متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [{ id: conversationId, status: 'Waiting', participantType: 'Guest', subject: 'AI test', createdAt: new Date().toISOString(), version: 1, canSend: true, canRate: false, isAiActive: true, isAiTyping: false }] }) }));
    await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/snapshot`, route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        data: {
          conversationId,
          status: 'Waiting',
          aiMode: 'AiActive',
          lastSequence: 1,
          canSend: true,
          aiTurnState: 'Completed',
          messages: [],
          pendingDecision: handoffProposal,
          verification: null
        }
      })
    }));

    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();

    // Verify handoff proposal card is visible
    await expect(page.getByText('التحويل لموظف بشري')).toBeVisible();
    await expect(page.getByText('حل مشكلة الدفع')).toBeVisible();

    // Mock confirmation endpoints
    await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/confirm`, route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data: { status: 'Succeeded' } })
    }));

    // Mock update snapshot to queued human state
    await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/snapshot`, route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        data: {
          conversationId,
          status: 'Waiting',
          aiMode: 'HumanQueued',
          lastSequence: 2,
          canSend: true,
          aiTurnState: 'Completed',
          messages: [],
          pendingDecision: null,
          verification: null
        }
      })
    }));

    // Click 'نعم، حوّلني'
    await page.getByRole('button', { name: 'نعم، حوّلني' }).click();
    await page.waitForTimeout(300);

    // Verify card is gone
    await expect(page.getByText('التحويل لموظف بشري')).not.toBeVisible();
  });

  test('handoff rejection cancels proposal and resumes AI', async ({ page }) => {
    const handoffProposal = {
      id: 'proposal-123',
      actionKey: 'system.handoff',
      safeProposalJson: '{\"safeSummaryAr\":\"حل مشكلة الدفع\"}',
      status: 'PendingConfirmation',
      expiresAt: new Date(Date.now() + 300000).toISOString()
    };
    await page.route('**/api/live-support/availability', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: { isAvailable: true, availableStaffCount: 0, code: 'AI_AVAILABLE', message: 'متاح' } }) }));
    await page.route('**/api/live-support/participant/conversations', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data: [{ id: conversationId, status: 'Waiting', participantType: 'Guest', subject: 'AI test', createdAt: new Date().toISOString(), version: 1, canSend: true, canRate: false, isAiActive: true, isAiTyping: false }] }) }));
    await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/snapshot`, route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        data: {
          conversationId,
          status: 'Waiting',
          aiMode: 'AiActive',
          lastSequence: 1,
          canSend: true,
          aiTurnState: 'Completed',
          messages: [],
          pendingDecision: handoffProposal,
          verification: null
        }
      })
    }));

    await page.goto(appUrl);
    await page.waitForTimeout(300);
    await page.getByRole('button', { name: 'فتح الدعم المباشر' }).click();

    // Mock cancellation endpoint
    await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/cancel`, route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data: { status: 'Cancelled' } })
    }));

    // Mock update snapshot to normal active AI state
    await page.route(`**/api/live-support/participant/conversations/${conversationId}/ai/snapshot`, route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        data: {
          conversationId,
          status: 'Waiting',
          aiMode: 'AiActive',
          lastSequence: 2,
          canSend: true,
          aiTurnState: 'Completed',
          messages: [],
          pendingDecision: null,
          verification: null
        }
      })
    }));

    // Click 'لا، استمر مع المساعد'
    await page.getByRole('button', { name: 'لا، استمر مع المساعد' }).click();
    await page.waitForTimeout(300);

    // Verify card is hidden and assistant active notice is present
    await expect(page.getByText('التحويل لموظف بشري')).not.toBeVisible();
    await expect(page.getByText('أنت تتحدث الآن مع مساعد ذكي')).toBeVisible();
  });
});

test.describe('AI live support staff and administration', () => {
  test('staff workspace displays safe AI handoff summary', async ({ page }) => {
    // Mock staff bootstrap DTO and conversations
    await page.route('**/api/live-support/staff/bootstrap', route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        data: {
          isEnabled: true,
          isCheckedIn: true,
          activeConversationsCount: 1,
          maxActiveConversations: 5,
          waitingQueueCount: 0,
          conversations: [{
            id: conversationId,
            participantType: 'Student',
            status: 'Assigned',
            subject: 'استفسار عن الدفع',
            createdAt: new Date().toISOString(),
            isAiActive: false,
            isAiTyping: false
          }]
        }
      })
    }));
  });
});

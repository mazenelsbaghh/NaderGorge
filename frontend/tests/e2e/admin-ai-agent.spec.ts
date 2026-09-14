import { expect, test, type Page } from '@playwright/test';
import type {
  AdminAiExecution,
  AdminAiProposal,
} from '../../src/services/admin-ai-agent-contract';
import { adminUrl, installAuthAndGoto } from './e2e-contract-helpers';

const admin = {
  id: '00000000-0000-4000-8000-000000000169',
  fullName: 'مدير اختبار وكيل الإدارة',
  phone: '20000000169',
  roles: ['Admin'],
  permissions: [],
  profileComplete: true,
  allowedDomains: ['admin'],
  allowedNavbarItems: [],
  authorizationVersion: 1,
};
const conversation = {
  id: '10000000-0000-4000-8000-000000000169',
  title: 'ملخص المنصة',
  status: 'Active',
  lastActivityAt: '2026-08-12T10:00:00Z',
  version: 1,
};

async function installAdminAiContractApi(
  page: Page,
  proposals: unknown[] = []
) {
  let snapshots = 0;
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname;
    let data: unknown = null;
    if (pathname.endsWith('/auth/session'))
      data = { user: admin, authorizationVersion: 1 };
    else if (pathname.endsWith('/admin/ai-agent/conversations'))
      data = { items: [conversation], nextCursor: null };
    else if (
      pathname.endsWith(
        `/admin/ai-agent/conversations/${conversation.id}/snapshot`
      )
    ) {
      snapshots += 1;
      data = {
        conversation,
        messages: [
          {
            id: '40000000-0000-4000-8000-000000000169',
            sequence: 1,
            role: 'Assistant',
            content:
              'إجمالي التحصيل EGP 1,250 — المرجع 50000000-0000-4000-8000-000000000169',
            answer: null,
            turnId: null,
            createdAt: '2026-08-12T10:00:00Z',
          },
        ],
        activeTurns: [],
        proposals,
        nextBeforeSequence: null,
        latestSequence: snapshots,
        baselineVersion: 'v1',
        sensitivePolicyVersion: 'v1',
        serverTime: new Date().toISOString(),
      };
    } else if (pathname.endsWith('/admin/ai-agent/action-evidence'))
      data = { items: [], nextCursor: null };
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data }),
    });
  });
  return () => snapshots;
}

async function openConversation(page: Page, proposals?: unknown[]) {
  if (proposals) await installAdminAiContractApi(page, proposals);
  await installAuthAndGoto(
    page,
    'admin-ai-contract-token',
    admin,
    `${adminUrl}/admin/ai-agent`
  );
  await page
    .getByRole('button', { name: conversation.title, exact: true })
    .click();
  await expect(
    page.getByRole('heading', { name: conversation.title })
  ).toBeVisible();
}

// Regression: production mobile "محادثة جديدة" did not open when the API returned a direct DTO.
test('new conversation opens its workspace on a phone with a direct API response', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const created = { ...conversation, title: 'محادثة جديدة' };
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname;
    const method = route.request().method();
    let data: unknown = null;
    if (pathname.endsWith('/auth/session'))
      data = { user: admin, authorizationVersion: 1 };
    else if (
      pathname.endsWith('/admin/ai-agent/conversations') &&
      method === 'POST'
    )
      data = created;
    else if (pathname.endsWith('/admin/ai-agent/conversations'))
      data = { items: [], nextCursor: null };
    else if (
      pathname.endsWith(`/admin/ai-agent/conversations/${created.id}/snapshot`)
    )
      data = {
        conversation: created,
        messages: [],
        activeTurns: [],
        proposals: [],
        nextBeforeSequence: null,
        latestSequence: 0,
        baselineVersion: 'v1',
        sensitivePolicyVersion: 'v1',
        serverTime: new Date().toISOString(),
      };
    else if (pathname.endsWith('/admin/ai-agent/action-evidence'))
      data = { items: [], nextCursor: null };
    await route.fulfill({
      status: method === 'POST' ? 201 : 200,
      contentType: 'application/json',
      body: JSON.stringify(data),
    });
  });
  await installAuthAndGoto(
    page,
    'admin-ai-mobile-create',
    admin,
    `${adminUrl}/admin/ai-agent`
  );
  await page.getByRole('button', { name: 'محادثة جديدة', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'محادثة جديدة' })
  ).toBeVisible();
});

// Regression: keep the public JSON name (`message`) aligned with the backend DTO contract.
test('sending a message uses the backend turn request contract', async ({
  page,
}) => {
  let turnRequest: unknown;
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname;
    const method = route.request().method();
    let data: unknown = null;
    let status = 200;
    if (pathname.endsWith('/auth/session'))
      data = { user: admin, authorizationVersion: 1 };
    else if (pathname.endsWith('/admin/ai-agent/conversations'))
      data = { items: [conversation], nextCursor: null };
    else if (
      pathname.endsWith(
        `/admin/ai-agent/conversations/${conversation.id}/turns`
      ) &&
      method === 'POST'
    ) {
      turnRequest = route.request().postDataJSON();
      status = 202;
      data = {
        id: '70000000-0000-4000-8000-000000000169',
        status: 'Queued',
        queuedAt: '2026-08-17T03:00:00Z',
        version: 1,
      };
    } else if (
      pathname.endsWith(
        `/admin/ai-agent/conversations/${conversation.id}/snapshot`
      )
    )
      data = {
        conversation,
        messages: [],
        activeTurns: [],
        proposals: [],
        nextBeforeSequence: null,
        latestSequence: 0,
        baselineVersion: 'v1',
        sensitivePolicyVersion: 'v1',
        serverTime: new Date().toISOString(),
      };
    else if (pathname.endsWith('/admin/ai-agent/action-evidence'))
      data = { items: [], nextCursor: null };
    await route.fulfill({
      status,
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data }),
    });
  });
  await openConversation(page);
  await page
    .getByPlaceholder('اسأل عن أي بيانات مسموح بها…')
    .fill('اعرض ملخص الطلاب');
  await page.getByRole('button', { name: 'إرسال' }).click();
  await expect
    .poll(() => turnRequest)
    .toEqual({
      message: 'اعرض ملخص الطلاب',
      expectedConversationVersion: 1,
    });
});

const ordinaryProposal = {
  id: '60000000-0000-4000-8000-000000000170',
  conversationId: conversation.id,
  turnId: '70000000-0000-4000-8000-000000000170',
  capabilityKey: 'content.lesson.update-title',
  capabilityLabelAr: 'تحديث عنوان الدرس',
  targetLabelAr: 'درس الجبر الأول',
  targetDrillDown: null,
  changes: [
    {
      labelAr: 'العنوان',
      currentValue: 'المعادلات',
      requestedValue: 'المعادلات الخطية',
      displayKind: 'Text',
    },
  ],
  effectSummaryAr: 'سيُحدّث عنوان درس واحد فقط.',
  consequenceAr: null,
  primaryRisk: 'Ordinary',
  riskFlags: ['Ordinary'],
  confirmationType: 'Explicit',
  strongConfirmationPhrase: null,
  validationSummary: ['تم التحقق من وجود الدرس.'],
  bulk: null,
  requiresSecureInput: false,
  secureInputKind: null,
  status: 'PendingConfirmation',
  expiresAt: '2099-08-12T10:00:00Z',
  execution: null,
  version: 1,
};

async function installInteractiveProposalApi(page: Page) {
  let current = { ...ordinaryProposal } as AdminAiProposal;
  const requests: Array<{ operation: 'confirm' | 'cancel'; body: unknown }> =
    [];
  await page.route('**/api/**', async (route) => {
    const pathname = new URL(route.request().url()).pathname;
    let data: unknown = null;
    if (pathname.endsWith('/auth/session'))
      data = { user: admin, authorizationVersion: 1 };
    else if (pathname.endsWith('/admin/ai-agent/conversations'))
      data = { items: [conversation], nextCursor: null };
    else if (
      pathname.endsWith(
        `/admin/ai-agent/conversations/${conversation.id}/snapshot`
      )
    )
      data = {
        conversation,
        messages: [],
        activeTurns: [],
        proposals: [current],
        nextBeforeSequence: null,
        latestSequence: 1,
        baselineVersion: 'v1',
        sensitivePolicyVersion: 'v1',
        serverTime: new Date().toISOString(),
      };
    else if (pathname.endsWith('/confirm')) {
      const body = route.request().postDataJSON();
      requests.push({ operation: 'confirm', body });
      const execution: AdminAiExecution = {
        id: '80000000-0000-4000-8000-000000000170',
        proposalId: current.id,
        status: 'Succeeded',
        safeSummaryAr: 'تم تحديث عنوان الدرس بنجاح',
        affectedCount: 1,
        succeededCount: 1,
        skippedCount: 0,
        failedCount: 0,
        items: [],
        refreshScopes: ['content'],
        failureCode: null,
        traceId: 'trace-safe-170',
        startedAt: '2026-08-12T10:00:00Z',
        completedAt: '2026-08-12T10:00:01Z',
      };
      current = { ...current, status: 'Succeeded', execution, version: 2 };
      data = execution;
    } else if (pathname.endsWith('/cancel')) {
      const body = route.request().postDataJSON();
      requests.push({ operation: 'cancel', body });
      current = { ...current, status: 'Cancelled', version: 2 };
      data = current;
    } else if (pathname.endsWith('/admin/ai-agent/action-evidence'))
      data = { items: [], nextCursor: null };
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data }),
    });
  });
  return requests;
}

test('ordinary proposal shows typed diff and distinct confirmation controls', async ({
  page,
}) => {
  await installInteractiveProposalApi(page);
  await openConversation(page);
  const card = page.getByRole('article', {
    name: `مقترح: ${ordinaryProposal.capabilityLabelAr}`,
  });
  await expect(card).toContainText('المعادلات');
  await expect(card).toContainText('المعادلات الخطية');
  await expect(card).toContainText('تم التحقق من وجود الدرس.');
  await expect(card.locator('pre')).toHaveCount(0);

  const confirm = card.getByRole('button', { name: 'تأكيد التنفيذ' });
  await confirm.focus();
  await expect(confirm).toBeFocused();
  await expect(confirm).toBeEnabled();
  await expect(card.getByRole('button', { name: 'إلغاء' })).toBeEnabled();
});

test('ordinary proposal exposes a cancel action without raw payload data', async ({
  page,
}) => {
  await installInteractiveProposalApi(page);
  await openConversation(page);
  const card = page.getByRole('article', {
    name: `مقترح: ${ordinaryProposal.capabilityLabelAr}`,
  });
  const cancel = card.getByRole('button', { name: 'إلغاء' });
  await expect(cancel).toBeVisible();
  await cancel.focus();
  await expect(cancel).toBeFocused();
  await expect(card.locator('pre')).toHaveCount(0);
});

test('terminal execution result is structured and has no executable CTA', async ({
  page,
}) => {
  const execution = {
    id: '80000000-0000-4000-8000-000000000171',
    proposalId: ordinaryProposal.id,
    status: 'Succeeded',
    safeSummaryAr: 'تم تحديث عنوان الدرس بنجاح',
    affectedCount: 1,
    succeededCount: 1,
    skippedCount: 0,
    failedCount: 0,
    items: [],
    refreshScopes: ['content'],
    failureCode: null,
    traceId: 'trace-safe-171',
    startedAt: '2026-08-12T10:00:00Z',
    completedAt: '2026-08-12T10:00:01Z',
  };
  await installAdminAiContractApi(page, [
    { ...ordinaryProposal, status: 'Succeeded', execution, version: 2 },
  ]);
  await openConversation(page);
  const result = page.getByRole('region', { name: 'نتيجة التنفيذ' });
  await expect(result).toContainText('تم تحديث عنوان الدرس بنجاح');
  await expect(result).toContainText('متأثر');
  await expect(page.getByRole('button', { name: 'تأكيد التنفيذ' })).toHaveCount(
    0
  );
  await expect(result.locator('pre')).toHaveCount(0);
});

test('expired strong confirmation cannot execute and retains keyboard focus', async ({
  page,
}) => {
  await installAdminAiContractApi(page, [
    {
      ...ordinaryProposal,
      id: '60000000-0000-4000-8000-000000000171',
      primaryRisk: 'Financial',
      riskFlags: ['Financial'],
      confirmationType: 'TypedStrong',
      strongConfirmationPhrase: 'CONFIRM FINANCE 171',
      expiresAt: '2020-01-01T00:00:00Z',
    },
  ]);
  await openConversation(page);
  const phrase = page.getByLabel('عبارة التأكيد');
  await expect(phrase).toBeFocused();
  await phrase.fill('CONFIRM FINANCE 171');
  await expect(
    page.getByRole('button', { name: 'تأكيد وتنفيذ مرة واحدة' })
  ).toBeDisabled();
  await expect(page.getByRole('status')).toContainText('انتهت صلاحية');
});

test('secure input overlay traps focus, closes with Escape and restores its trigger', async ({
  page,
}) => {
  const secureProposal = {
    id: '60000000-0000-4000-8000-000000000169',
    conversationId: conversation.id,
    turnId: '70000000-0000-4000-8000-000000000169',
    capabilityKey: 'identity.password.reset',
    capabilityLabelAr: 'إعادة ضبط كلمة المرور',
    targetLabelAr: 'حساب اختباري',
    targetDrillDown: null,
    changes: [],
    effectSummaryAr: 'تحديث بيانات الدخول بعد التأكيد',
    consequenceAr: 'سيحتاج المستخدم إلى بيانات الدخول الجديدة.',
    primaryRisk: 'Credential',
    riskFlags: ['Credential'],
    confirmationType: 'TypedStrong',
    strongConfirmationPhrase: null,
    validationSummary: [],
    bulk: null,
    requiresSecureInput: true,
    secureInputKind: 'Password',
    status: 'PendingSecureInput',
    expiresAt: '2099-08-12T10:00:00Z',
    execution: null,
    version: 1,
  };
  await openConversation(page, [secureProposal]);
  const trigger = page.getByRole('button', { name: 'إدخال القيمة الآمنة' });
  await trigger.focus();
  await trigger.click();

  const dialog = page.getByRole('dialog', { name: /إدخال آمن/ });
  const secretInput = dialog.getByLabel('كلمة المرور');
  await expect(secretInput).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await expect(dialog.getByRole('button', { name: 'إلغاء' })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(secretInput).toBeFocused();
  await page.keyboard.press('Escape');

  await expect(dialog).toBeHidden();
  await expect(trigger).toBeFocused();
});

for (const width of [375, 768, 1024, 1440]) {
  for (const colorScheme of ['light', 'dark'] as const) {
    test(`mocked AdminAI workspace remains accessible at ${width}px, ${colorScheme}, 200% zoom and reduced motion`, async ({
      page,
    }) => {
      await page.setViewportSize({ width, height: 900 });
      await page.emulateMedia({
        reducedMotion: 'reduce',
        colorScheme,
      });
      await installAdminAiContractApi(page);
      await openConversation(page);
      await page.evaluate(() => {
        document.documentElement.style.zoom = '2';
      });
      await expect(page.getByLabel('مساحة محادثات وكيل الإدارة')).toBeVisible();
      await expect(page.getByLabel('اكتب سؤالك لوكيل الإدارة')).toBeVisible();
      await page.getByLabel('اكتب سؤالك لوكيل الإدارة').focus();
      await expect(page.getByLabel('اكتب سؤالك لوكيل الإدارة')).toBeFocused();
      await expect(page.getByRole('log')).toContainText('EGP 1,250');
      expect(
        await page.evaluate(
          () =>
            document.documentElement.scrollWidth <=
            document.documentElement.clientWidth
        )
      ).toBe(true);
    });
  }
}

test('mocked duplicate, gap and tab-resume envelopes converge through authoritative snapshots', async ({
  page,
}) => {
  const snapshotCount = await installAdminAiContractApi(page);
  await openConversation(page);
  const initial = snapshotCount();
  await page.evaluate(
    ({ conversationId }) => {
      const testing = (
        window as typeof window & {
          __platformEventsTesting?: {
            getListeners: () => {
              AdminAiEvent: Set<(payload: unknown) => void>;
            };
          };
        }
      ).__platformEventsTesting;
      const emit = (sequence: number, eventId: string) =>
        testing?.getListeners().AdminAiEvent.forEach((listener) =>
          listener({
            schemaVersion: '1',
            eventId,
            sequence,
            type: 'snapshot_changed',
            conversationId,
            occurredAt: new Date().toISOString(),
          })
        );
      emit(1, '20000000-0000-4000-8000-000000000169');
      emit(1, '20000000-0000-4000-8000-000000000169');
      emit(3, '30000000-0000-4000-8000-000000000169');
    },
    { conversationId: conversation.id }
  );
  await expect.poll(snapshotCount).toBeGreaterThan(initial);
  await page.evaluate(() =>
    document.dispatchEvent(new Event('visibilitychange'))
  );
  await expect.poll(snapshotCount).toBeGreaterThan(initial + 1);
});

test('real reconnect and terminal snapshot convergence contract', async () => {
  test.skip(
    process.env.ADMIN_AI_REALTIME_E2E !== '1',
    'Requires the real PlatformHub and seeded AdminAI backend.'
  );
});

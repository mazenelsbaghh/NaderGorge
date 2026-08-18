import assert from 'node:assert/strict';
import test from 'node:test';
import {
  ADMIN_AI_ROUTE_BUILDERS,
  adminAiRequestConfig,
  adminAiAgentPaths,
  normalizeAdminAiSnapshot,
  parseAdminAiApiError,
  parseAdminAiErrorResponse,
  unwrapAdminAiPayload,
  type AdminAiRouteKey,
} from './admin-ai-agent-contract.ts';

test('request config preserves AbortSignal and only sends explicit idempotency keys', () => {
  const signal = new AbortController().signal;
  assert.deepEqual(adminAiRequestConfig(signal), {
    signal,
    suppressErrorToast: true,
    headers: undefined,
  });
  assert.deepEqual(adminAiRequestConfig(signal, 'intent-key'), {
    signal,
    suppressErrorToast: true,
    headers: { 'Idempotency-Key': 'intent-key' },
  });
});

test('Admin AI payload unwrap supports direct and enveloped API representations', () => {
  const payload = { id: 'conversation-id', title: 'محادثة جديدة' };
  assert.deepEqual(unwrapAdminAiPayload<typeof payload>(payload), payload);
  assert.deepEqual(
    unwrapAdminAiPayload<typeof payload>({ data: payload }),
    payload
  );
});

test('API errors accept the closed safe shape and reject additions or unknown codes', () => {
  const safe = {
    code: 'RATE_LIMITED',
    messageAr: 'حاول لاحقًا',
    retryAfterSeconds: 30,
    traceId: 'trace-1',
    currentVersion: null,
  };
  assert.deepEqual(parseAdminAiApiError(safe), safe);
  assert.equal(parseAdminAiApiError({ ...safe, detail: 'secret' }), undefined);
  assert.equal(
    parseAdminAiApiError({ ...safe, code: 'MODEL_RAW_ERROR' }),
    undefined
  );
});

test('production API errors are normalized without exposing unexpected fields', () => {
  assert.deepEqual(
    parseAdminAiErrorResponse({
      code: 'ACTIVE_TURN_LIMIT',
      message: 'تعذر إكمال الطلب بأمان.',
      retryable: false,
    }),
    {
      code: 'ACTIVE_TURN_LIMIT',
      messageAr: 'لديك محادثتان قيد الرد. انتظر اكتمال إحداهما ثم أرسل سؤالك.',
      retryAfterSeconds: null,
      traceId: '',
      currentVersion: null,
    }
  );
  assert.equal(
    parseAdminAiErrorResponse({
      code: 'ACTIVE_TURN_LIMIT',
      message: 'safe',
      retryable: false,
      privateDetail: 'must-not-pass',
    }),
    undefined
  );
});

test('current snapshot shape exposes its active turn to the workspace', () => {
  const snapshot = {
    conversation: {
      id: 'conversation-id',
      title: 'محادثة جديدة',
      status: 'Active' as const,
      lastActivityAt: '2026-08-17T00:00:00Z',
      version: 2,
    },
    messages: [],
    activeTurn: {
      id: 'turn-id',
      status: 'Queued' as const,
      queuedAt: '2026-08-17T00:00:00Z',
      version: 1,
    },
  };
  assert.deepEqual(normalizeAdminAiSnapshot(snapshot).turns, [
    snapshot.activeTurn,
  ]);
});

test('resource identifiers are encoded in every Admin AI endpoint', () => {
  const unsafeId = 'id/with?query=#fragment';
  for (const path of [
    adminAiAgentPaths.conversation(unsafeId),
    adminAiAgentPaths.archiveConversation(unsafeId),
    adminAiAgentPaths.restoreConversation(unsafeId),
    adminAiAgentPaths.snapshot(unsafeId),
    adminAiAgentPaths.turns(unsafeId),
    adminAiAgentPaths.proposal(unsafeId),
  ]) {
    assert.equal(path.includes('?query='), false);
    assert.equal(path.includes('#fragment'), false);
    assert.match(path, /id%2Fwith%3Fquery%3D%23fragment/);
  }
});

test('drill-down links only use the closed route-key registry', () => {
  const keys = Object.keys(ADMIN_AI_ROUTE_BUILDERS) as AdminAiRouteKey[];
  assert.deepEqual(keys.sort(), [
    'admin.assessment.exam',
    'admin.content.lesson',
    'admin.finance.transaction',
    'admin.hr.employee',
    'admin.student.details',
    'admin.support.conversation',
    'admin.teacher.details',
  ]);
  assert.equal(ADMIN_AI_ROUTE_BUILDERS['admin.student.details']({}), null);
  assert.equal(
    ADMIN_AI_ROUTE_BUILDERS['admin.student.details']({ id: '../secret' }),
    '/admin/students/..%2Fsecret'
  );
});

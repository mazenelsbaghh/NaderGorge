import assert from 'node:assert/strict';
import test from 'node:test';
import {
  ADMIN_AI_ROUTE_BUILDERS,
  adminAiRequestConfig,
  adminAiAgentPaths,
  parseAdminAiApiError,
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
  assert.equal(parseAdminAiApiError({ ...safe, code: 'MODEL_RAW_ERROR' }), undefined);
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

import assert from 'node:assert/strict';
import test from 'node:test';
import { permissionsForRefundPage } from './refund-role-permissions.ts';

test('2026-09-21 saving the enabled staff refund page supplies its missing permissions without reversal access', () => {
  const original = ['users.manage', 'finance.refunds.view'];
  const saved = permissionsForRefundPage(original, 'assistant', ['/assistant/refunds']);
  assert.deepEqual(saved, ['users.manage', 'finance.refunds.view', 'finance.refunds.create']);
  assert.deepEqual(original, ['users.manage', 'finance.refunds.view']);
  assert.equal(saved.includes('finance.refunds.post'), false);
});

for (const [domain, pages] of [
  ['assistant', []],
  ['assistant', ['/assistant/students']],
  ['admin', ['/assistant/refunds']],
] as const) {
  test(`saving ${domain} with ${pages.join(',') || 'no pages'} does not grant refund access`, () => {
    assert.deepEqual(permissionsForRefundPage(['users.manage'], domain, [...pages]), ['users.manage']);
  });
}

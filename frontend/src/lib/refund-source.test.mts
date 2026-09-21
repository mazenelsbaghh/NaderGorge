import assert from 'node:assert/strict';
import test from 'node:test';

import {
  isExternallyRefundableGrant,
  refundablePurchaseOperationId,
  refundSourceKey,
} from './refund-source.ts';

const grant = {
  accessGrantId: 'grant-1',
  isActive: true,
  purchaseMethod: 'Balance',
  price: 999,
  purchaseOperationId: 'purchase-1',
  paidAmount: 999,
};

test('cash-backed purchases keep their financial purchase source', () => {
  assert.equal(refundablePurchaseOperationId(grant), 'purchase-1');
  assert.equal(refundSourceKey(grant), 'purchase-1');
  assert.equal(isExternallyRefundableGrant(grant), true);
});

test('2026-09-21 zero-cash balance purchases retain their sale for manual review', () => {
  const promotionalPurchase = { ...grant, paidAmount: 0, price: 0 };
  assert.equal(refundablePurchaseOperationId(promotionalPurchase), 'purchase-1');
  assert.equal(refundSourceKey(promotionalPurchase), 'purchase-1');
  assert.equal(isExternallyRefundableGrant(promotionalPurchase), true);
});

test('2026-09-21 code and gift grants use their content price as the manual cash ceiling', () => {
  assert.equal(
    isExternallyRefundableGrant({ ...grant, purchaseMethod: 'Gift' }),
    true
  );
  assert.equal(
    isExternallyRefundableGrant({ ...grant, purchaseMethod: 'Code' }),
    true
  );
});

test('inactive and valueless grants remain ineligible', () => {
  assert.equal(
    isExternallyRefundableGrant({ ...grant, isActive: false }),
    false
  );
  assert.equal(
    isExternallyRefundableGrant({
      ...grant,
      paidAmount: 0,
      price: 0,
      purchaseOperationId: null,
    }),
    false
  );
});

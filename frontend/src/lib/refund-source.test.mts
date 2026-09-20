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

test('2026-09-21 zero-cash direct purchases use the reviewed external refund path', () => {
  const promotionalPurchase = { ...grant, paidAmount: 0 };
  assert.equal(refundablePurchaseOperationId(promotionalPurchase), undefined);
  assert.equal(refundSourceKey(promotionalPurchase), 'historical:grant-1');
  assert.equal(isExternallyRefundableGrant(promotionalPurchase), true);
});

test('gift, code, inactive, and valueless grants remain ineligible', () => {
  assert.equal(
    isExternallyRefundableGrant({ ...grant, purchaseMethod: 'Gift' }),
    false
  );
  assert.equal(
    isExternallyRefundableGrant({ ...grant, purchaseMethod: 'Code' }),
    false
  );
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

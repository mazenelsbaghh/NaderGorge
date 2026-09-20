export type RefundableGrantSource = {
  accessGrantId: string;
  isActive: boolean;
  purchaseMethod: string;
  price: number;
  purchaseOperationId?: string | null;
  paidAmount: number;
};

const isGiftOrCode = (grant: RefundableGrantSource) =>
  grant.purchaseMethod === 'Code' || grant.purchaseMethod === 'Gift';

export function refundablePurchaseOperationId(
  grant: RefundableGrantSource
): string | undefined {
  if (isGiftOrCode(grant) || grant.paidAmount <= 0) return undefined;
  return grant.purchaseOperationId || undefined;
}

export function isExternallyRefundableGrant(
  grant: RefundableGrantSource
): boolean {
  if (!grant.isActive || isGiftOrCode(grant)) return false;
  return Boolean(refundablePurchaseOperationId(grant)) || grant.price > 0;
}

export function refundSourceKey(grant: RefundableGrantSource): string {
  return (
    refundablePurchaseOperationId(grant) || `historical:${grant.accessGrantId}`
  );
}

/** Keep a teacher coupon's academic target aligned with the selected teacher. */
export function prepareSalesCouponPayload(payload: Record<string, unknown>): Record<string, unknown> {
  if (payload.targetType !== 'Teacher') return payload;

  const teacherId = typeof payload.teacherId === 'string' ? payload.teacherId.trim() : '';
  if (!teacherId) {
    throw new Error('اختيار المدرس مطلوب عندما يكون هدف الخصم مدرسًا.');
  }

  return { ...payload, teacherId, targetId: teacherId };
}

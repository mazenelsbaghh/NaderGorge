'use client';

import { useState } from 'react';
import { Ban, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminGiftsService, giftTargetLabels, type GiftDetailsDto } from '@/services/admin-gifts-service';
import { GiftStatusBadge } from './GiftStatusBadge';

export function GiftDetailsPanel({ gift, onChanged }: { gift: GiftDetailsDto; onChanged: () => Promise<void> }) {
  const [showRevoke, setShowRevoke] = useState(false);
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const valueLabel = gift.originalValue == null
    ? `${gift.recipients.reduce((sum, item) => sum + item.usesConsumed, 0)} استخدام`
    : gift.availableValue == null
      ? `إجمالي ${gift.originalValue} ج.م`
      : `${gift.availableValue} / ${gift.originalValue} ج.م متاح`;

  const revoke = async () => {
    if (!reason.trim()) return toast.error('سبب الإلغاء مطلوب.');
    try {
      setSaving(true);
      const result = await adminGiftsService.revoke(gift.id, reason.trim());
      toast.success(result.changed ? 'تم إلغاء المتبقي من الهدية.' : 'لا يوجد متبقٍ جديد للإلغاء.');
      setShowRevoke(false);
      await onChanged();
    } finally { setSaving(false); }
  };

  return <div className="space-y-6">
    <section className="grid gap-4 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 md:grid-cols-2 lg:grid-cols-4">
      <div><small className="text-[var(--admin-muted)]">الهدف</small><strong className="mt-1 block text-[var(--admin-text)]">{gift.targetName}</strong><span className="text-xs text-[var(--admin-muted)]">{giftTargetLabels[gift.targetType]}</span></div>
      <div><small className="text-[var(--admin-muted)]">الحالة</small><div className="mt-2"><GiftStatusBadge status={gift.status} /></div></div>
      <div><small className="text-[var(--admin-muted)]">المُصدر</small><strong className="mt-1 block text-[var(--admin-text)]">{gift.issuerName}</strong></div>
      <div><small className="text-[var(--admin-muted)]">الاستخدام/القيمة</small><strong className="mt-1 block text-[var(--admin-text)]">{valueLabel}</strong></div>
      <div className="md:col-span-2 lg:col-span-4"><small className="text-[var(--admin-muted)]">سبب الإصدار</small><p className="mt-1 text-sm font-medium text-[var(--admin-text)]">{gift.reason}</p></div>
    </section>

    <section className="overflow-x-auto rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)]">
      <table className="w-full min-w-[700px] text-right text-sm"><thead className="bg-[var(--admin-card-soft)] text-[var(--admin-muted)]"><tr><th className="px-4 py-3">الطالب</th><th className="px-4 py-3">النتيجة</th><th className="px-4 py-3">الاستخدام</th><th className="px-4 py-3">التفاصيل</th></tr></thead><tbody className="divide-y divide-[var(--admin-border)]">{gift.recipients.map((recipient) => <tr key={recipient.studentId}><td className="px-4 py-3 font-bold text-[var(--admin-text)]">{recipient.studentName}</td><td className="px-4 py-3"><GiftStatusBadge status={recipient.status} /></td><td className="px-4 py-3">{recipient.usesConsumed}{recipient.maxUses ? ` / ${recipient.maxUses}` : ''}</td><td className="px-4 py-3 text-[var(--admin-muted)]">{recipient.outcomeMessage ?? recipient.outcomeCode}</td></tr>)}</tbody></table>
    </section>

    {gift.status !== 'Revoked' ? <button type="button" onClick={() => setShowRevoke(true)} className="inline-flex h-11 items-center gap-2 rounded-lg border border-red-500/30 px-4 text-sm font-bold text-red-600 hover:bg-red-500/10"><Ban className="h-4 w-4" /> إلغاء المتبقي</button> : null}

    {showRevoke ? <div className="fixed inset-0 z-[var(--z-overlay)] flex items-center justify-center bg-black/45 p-4"><div role="dialog" aria-modal="true" aria-labelledby="revoke-title" className="w-full max-w-lg rounded-lg bg-[var(--admin-card)] p-5 shadow-2xl"><h2 id="revoke-title" className="text-lg font-black text-[var(--admin-text)]">إلغاء المتبقي من الهدية</h2><p className="mt-2 text-sm text-[var(--admin-muted)]">الاستخدام السابق سيظل محفوظاً، وسيُمنع فقط الوصول أو الرصيد غير المستخدم.</p><textarea autoFocus value={reason} onChange={(event) => setReason(event.target.value)} className="admin-input mt-4 min-h-24" maxLength={500} placeholder="سبب الإلغاء" /><div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => setShowRevoke(false)} className="h-10 rounded-lg px-4 text-sm font-bold text-[var(--admin-muted)]">تراجع</button><button type="button" onClick={() => void revoke()} disabled={saving || !reason.trim()} className="inline-flex h-10 items-center gap-2 rounded-lg bg-red-600 px-4 text-sm font-bold text-white disabled:opacity-50">{saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Ban className="h-4 w-4" />} تأكيد الإلغاء</button></div></div></div> : null}
  </div>;
}

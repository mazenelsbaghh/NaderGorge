'use client';

import { useCallback, useEffect, useState } from 'react';
import { AlertTriangle, ArrowRightLeft, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminPage } from '@/components/admin';
import { walletService, type RechargeMessageConflictDto, type RechargeSmsSuggestionDto } from '@/services/wallet-service';

export function RechargeConflictsWorkspace() {
  const [items, setItems] = useState<RechargeMessageConflictDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<{ conflict: RechargeMessageConflictDto; sms: RechargeSmsSuggestionDto } | null>(null);
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try { setItems(await walletService.getRechargeMessageConflicts()); }
    catch { toast.error('تعذر تحميل تعارضات رسائل الشحن'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  async function reassign() {
    if (!selected || !reason.trim()) return;
    setSaving(true);
    try {
      const result = await walletService.reassignRechargeSms(selected.conflict.rechargeRequestId, selected.sms.smsLogId, reason.trim());
      toast.success(result.message);
      setSelected(null);
      setReason('');
      await load();
    } catch { toast.error('تعذر نقل الربط. راجع رصيد الطالب القديم وبيانات التحويل.'); }
    finally { setSaving(false); }
  }

  return <div className="space-y-4" dir="rtl">
    <div className="flex items-center justify-between"><p className="text-sm text-[var(--admin-muted)]">لا يتم النقل تلقائيًا؛ راجع الحساب الكامل ورقم العملية ثم اكتب سبب النقل.</p><button type="button" className="admin-btn-ghost" onClick={() => void load()}><RefreshCw className="h-4 w-4" /> تحديث</button></div>
    {loading ? <p className="admin-panel rounded-2xl p-10 text-center">جارٍ المراجعة…</p> : null}
    {!loading && items.length === 0 ? <p className="admin-panel rounded-2xl p-10 text-center text-emerald-700">لا توجد تعارضات تحتاج تدخلاً حاليًا.</p> : null}
    {items.map(conflict => <section key={conflict.rechargeRequestId} className="admin-panel rounded-2xl p-5">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3"><div><h2 className="flex items-center gap-2 font-black"><AlertTriangle className="h-5 w-5 text-amber-600" />{conflict.studentName}</h2><bdi className="font-mono text-sm">{conflict.studentPhoneNumber}</bdi><p className="mt-1 text-sm text-amber-800">{conflict.conflictDescription}</p></div><div className="text-left"><b>{conflict.amount.toLocaleString('ar-EG')} ج.م</b><bdi className="block font-mono text-xs">من {conflict.senderPhoneNumber}</bdi><span className="text-xs">المحجوزة: {conflict.walletLabel}</span></div></div>
      <div className="grid gap-3 xl:grid-cols-2">{conflict.candidates.map(sms => <article key={sms.smsLogId} className="rounded-xl border border-[var(--admin-border)] p-4">
        <div className="flex justify-between gap-2"><b>{sms.walletLabel} — {sms.amount ?? '—'} ج.م</b><span className="rounded-full bg-amber-100 px-2 py-1 text-xs font-bold">تطابق {sms.matchScore}%</span></div>
        <bdi className="mt-2 block font-mono text-sm">المحول: {sms.senderPhoneNumber}</bdi><bdi className="block font-mono text-xs">رقم العملية: {sms.transferReference || 'غير مستخرج'}</bdi><span className="block text-xs text-[var(--admin-muted)]">{new Date(sms.receivedAt).toLocaleString('ar-EG', { timeZone: 'Africa/Cairo' })}</span>
        {sms.isMatched ? <div className="mt-2 rounded-lg bg-rose-50 p-2 text-sm text-rose-800">مرتبطة حاليًا بـ <b>{sms.matchedStudentName}</b> — <bdi className="font-mono">{sms.matchedStudentPhoneNumber}</bdi></div> : <div className="mt-2 rounded-lg bg-emerald-50 p-2 text-sm text-emerald-800">غير مرتبطة ويمكن ربطها من شاشة المطابقة.</div>}
        {sms.isMatched && sms.matchedRechargeRequestId !== conflict.rechargeRequestId ? <button type="button" className="admin-btn-ghost mt-3" onClick={() => setSelected({ conflict, sms })}><ArrowRightLeft className="h-4 w-4" /> مراجعة ونقل الربط</button> : null}
      </article>)}</div>
    </section>)}
    {selected ? <div className="fixed inset-0 z-[var(--z-modal)] grid place-items-center bg-black/50 p-4"><div className="w-full max-w-lg rounded-2xl bg-[var(--admin-card)] p-5 shadow-2xl"><h2 className="text-lg font-black">تأكيد نقل ربط التحويل</h2><p className="mt-2 text-sm">من <b>{selected.sms.matchedStudentName}</b> ({selected.sms.matchedStudentPhoneNumber}) إلى <b>{selected.conflict.studentName}</b> ({selected.conflict.studentPhoneNumber}). سيتم عكس الرصيد والقيد القديم أولًا ثم شحن الحساب الصحيح داخل معاملة واحدة.</p><textarea className="admin-input mt-4 min-h-24" value={reason} onChange={event => setReason(event.target.value)} placeholder="سبب النقل ومرجع المراجعة (مطلوب)" /><div className="mt-4 flex gap-2"><button className="admin-btn-primary" disabled={saving || !reason.trim()} onClick={() => void reassign()}>تأكيد النقل الآمن</button><button className="admin-btn-ghost" disabled={saving} onClick={() => { setSelected(null); setReason(''); }}>إلغاء</button></div></div></div> : null}
  </div>;
}

export default function RechargeConflictsPageClient() {
  return <AdminPage activePath="/admin/recharge-conflicts" sectionLabel="المدفوعات" pageTitle="تعارضات رسائل الشحن" subtitle="مراجعة الرسائل المرتبطة بطلب غير صحيح ونقلها بأثر محاسبي آمن."><RechargeConflictsWorkspace /></AdminPage>;
}

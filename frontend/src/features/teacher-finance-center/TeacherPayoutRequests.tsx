'use client';

import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { AdminModal } from '@/components/admin';
import { financeService, type AdminPayoutDto } from '@/services/finance-service';
import { formatCairoDateTime } from '@/lib/cairo-time';
import { teacherMoney } from './TeacherAccountOverview';

const PAID = 1;
const APPROVED = 3;
const statusLabels: Record<string, string> = { Pending: 'في انتظار الموافقة', Approved: 'معتمد ولم يُصرف', Paid: 'تم الصرف', Rejected: 'مرفوض' };

export function TeacherPayoutRequests({ teacherId, onChanged }: { teacherId: string; onChanged: () => void }) {
  const [rows, setRows] = useState<AdminPayoutDto[] | null>(null);
  const [error, setError] = useState(false);
  const [version, setVersion] = useState(0);
  const [action, setAction] = useState<{ payout: AdminPayoutDto; status: number } | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    let active = true;
    setRows(null); setError(false);
    void financeService.getPayouts(undefined, teacherId).then(data => { if (active) setRows(data); })
      .catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, [teacherId, version]);
  const resolve = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!action || busy) return;
    setBusy(true);
    try {
      const result = await financeService.resolvePayout(action.payout.id, { status: action.status, rejectionReason: reason.trim() || undefined });
      if (!result.success) { toast.error(result.message || 'تعذر تحديث الطلب'); return; }
      setAction(null); setReason(''); setVersion(value => value + 1); onChanged();
      toast.success(result.message || 'تم تحديث الطلب');
    } catch { toast.error('تعذر تحديث طلب السحب. حدّث الحساب قبل إعادة المحاولة.'); }
    finally { setBusy(false); }
  };
  return <section className="space-y-4" aria-label="طلبات سحب المدرس">
    <p className="text-sm leading-6 text-[var(--admin-muted)]">طلبات السحب بتحجز المبلغ من نفس الحساب. الموافقة مش صرف؛ سجّل الصرف بعد تحويل الفلوس فعليًا.</p>
    {error ? <p role="alert">تعذر تحميل الطلبات. <button className="min-h-11 px-3 underline" onClick={() => setVersion(value => value + 1)}>إعادة المحاولة</button></p> : !rows ? <p role="status">جارٍ تحميل الطلبات...</p> : !rows.length ? <p>لا توجد طلبات سحب.</p> : <ul className="divide-y divide-[var(--admin-border)]">{rows.map(payout => <li key={payout.id} className="flex flex-wrap items-center justify-between gap-4 py-4">
      <div><p className="font-bold tabular-nums">{teacherMoney(payout.amount)}</p><p className="mt-1 text-sm text-[var(--admin-muted)]">{formatCairoDateTime(payout.createdAt, { dateStyle: 'medium' })} · {statusLabels[payout.status] ?? payout.status}</p>{payout.rejectionReason && <p className="text-sm">{payout.rejectionReason}</p>}</div>
      <div className="flex flex-wrap gap-2">
        {payout.status === 'Pending' && <button className="admin-btn-ghost min-h-11 px-3" onClick={() => setAction({ payout, status: APPROVED })}>موافقة على الطلب</button>}
        {payout.status === 'Approved' && <button className="admin-btn-ghost min-h-11 px-3" onClick={() => setAction({ payout, status: PAID })}>تسجيل الصرف الفعلي</button>}
        {['Pending', 'Approved'].includes(String(payout.status)) && <button className="min-h-11 px-3 underline" onClick={() => { setReason(''); setAction({ payout, status: 2 }); }}>رفض وفك الحجز</button>}
      </div>
    </li>)}</ul>}
    <AdminModal open={!!action} onClose={() => { if (!busy) setAction(null); }} title={action?.status === PAID ? 'تأكيد الصرف الفعلي' : action?.status === 2 ? 'رفض طلب السحب' : 'الموافقة على طلب السحب'}>
      {action && <form onSubmit={resolve} className="space-y-4"><p>{action.status === PAID ? 'تأكد إن المدرّس استلم المبلغ قبل التسجيل:' : 'مبلغ الطلب:'} <strong>{teacherMoney(action.payout.amount)}</strong></p>
        {action.status === 2 && <label className="block">سبب الرفض<input required value={reason} onChange={event => setReason(event.target.value)} className="mt-2 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3" /></label>}
        <button disabled={busy} className="admin-btn-primary min-h-11 px-5" type="submit">{busy ? 'جارٍ الحفظ...' : 'تأكيد'}</button>
      </form>}
    </AdminModal>
  </section>;
}

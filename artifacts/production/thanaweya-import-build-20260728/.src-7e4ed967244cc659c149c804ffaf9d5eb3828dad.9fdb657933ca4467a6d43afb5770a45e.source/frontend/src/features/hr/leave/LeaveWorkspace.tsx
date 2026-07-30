'use client';

import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { CalendarDays, Loader2, Paperclip, Send, WalletCards } from 'lucide-react';
import toast from 'react-hot-toast';
import { hrService, LeaveBalanceDto, LeaveRequestDto, LeaveTypeDto } from '@/services/hr-service';
import { HrStatusBadge } from '@/features/hr/components/HrStatusBadge';

const emptyRequest = {
  leaveTypeId: '',
  startDate: '',
  endDate: '',
  dayFraction: 1,
  reason: '',
  attachmentReference: '',
};

export function LeaveWorkspace() {
  const [types, setTypes] = useState<LeaveTypeDto[]>([]);
  const [balances, setBalances] = useState<LeaveBalanceDto[]>([]);
  const [requests, setRequests] = useState<LeaveRequestDto[]>([]);
  const [form, setForm] = useState(emptyRequest);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const selectedType = useMemo(
    () => types.find((leaveType) => leaveType.id === form.leaveTypeId),
    [form.leaveTypeId, types]
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [catalog, balanceRows, requestRows] = await Promise.all([
        hrService.listLeaveTypes(),
        hrService.listLeaveBalances(),
        hrService.listMyLeaveRequests(),
      ]);
      setTypes(catalog);
      setBalances(balanceRows);
      setRequests(requestRows);
      setForm((current) => ({ ...current, leaveTypeId: current.leaveTypeId || catalog[0]?.id || '' }));
    } catch {
      toast.error('تعذر تحميل بيانات الإجازات');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (form.dayFraction === 0.5 && form.startDate !== form.endDate) {
      toast.error('نصف اليوم يجب أن يكون في تاريخ واحد');
      return;
    }
    if (selectedType?.requiresAttachment && !form.attachmentReference.trim()) {
      toast.error('أضف رابط أو رقم المرفق المطلوب');
      return;
    }
    setSaving(true);
    try {
      await hrService.submitLeaveRequest({
        ...form,
        attachmentReference: form.attachmentReference.trim() || null,
      });
      toast.success('تم إرسال الطلب إلى مسار الموافقات وحجز الرصيد');
      setForm({ ...emptyRequest, leaveTypeId: form.leaveTypeId });
      await load();
    } catch {
      toast.error('تعذر إرسال الطلب. راجع الرصيد والسياسة ومسار الموافقات.');
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="hr-loading" role="status">
        <Loader2 className="mx-auto h-6 w-6 animate-spin text-[var(--admin-accent)]" />
        <p className="mt-3">جارٍ تحميل أرصدة الإجازات…</p>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <section aria-label="أرصدة الإجازات" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {balances.length === 0 ? (
          <div className="hr-empty sm:col-span-2 lg:col-span-3">
            لم تُضف أرصدة إجازات إلى ملفك بعد. تواصل مع الموارد البشرية إذا كنت تتوقع رصيدًا.
          </div>
        ) : balances.map((balance) => (
          <article key={balance.id} className="hr-panel hr-panel--accent">
            <div className="flex items-start justify-between">
              <div>
                <p className="text-sm font-bold text-[var(--admin-muted)]">{balance.leaveType}</p>
                <p className="mt-2 text-3xl font-black text-[var(--admin-text)]">
                  {balance.available}
                  <span className="mr-1 text-sm text-[var(--admin-muted)]">يوم متاح</span>
                </p>
              </div>
              <span className="hr-icon">
                <WalletCards className="h-5 w-5" aria-hidden="true" />
              </span>
            </div>
            <p className="mt-3 text-xs font-bold text-[var(--admin-muted)]">
              محجوز {balance.reserved} · مستخدم {balance.used}
            </p>
          </article>
        ))}
      </section>

      <form onSubmit={submit} className="hr-panel">
        <div className="mb-5 flex items-center gap-3">
          <span className="hr-icon">
            <CalendarDays className="h-5 w-5" aria-hidden="true" />
          </span>
          <div>
            <h2 className="text-lg font-black">طلب إجازة جديد</h2>
            <p className="text-sm text-[var(--admin-muted)]">يُحجز الرصيد عند الإرسال ويُخصم بعد الاعتماد النهائي.</p>
          </div>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <label className="text-sm font-bold">
            نوع الإجازة
            <select required value={form.leaveTypeId} onChange={(event) => setForm({ ...form, leaveTypeId: event.target.value, attachmentReference: '' })} className="admin-input mt-2 min-h-11">
              {types.map((leaveType) => <option key={leaveType.id} value={leaveType.id}>{leaveType.name}</option>)}
            </select>
          </label>
          <label className="text-sm font-bold">
            المدة
            <select value={form.dayFraction} onChange={(event) => setForm({ ...form, dayFraction: Number(event.target.value) })} className="admin-input mt-2 min-h-11">
              <option value={1}>يوم كامل</option>
              {selectedType?.allowsHalfDay && <option value={0.5}>نصف يوم</option>}
            </select>
          </label>
          <label className="text-sm font-bold">
            من
            <input required type="date" value={form.startDate} onChange={(event) => setForm({ ...form, startDate: event.target.value, endDate: form.dayFraction === 0.5 ? event.target.value : form.endDate })} className="admin-input mt-2 min-h-11" />
          </label>
          <label className="text-sm font-bold">
            إلى
            <input required disabled={form.dayFraction === 0.5} type="date" min={form.startDate} value={form.endDate} onChange={(event) => setForm({ ...form, endDate: event.target.value })} className="admin-input mt-2 min-h-11" />
          </label>
          {selectedType?.requiresAttachment && (
            <label className="text-sm font-bold md:col-span-2">
              المرفق المطلوب
              <span className="relative mt-2 block">
                <Paperclip className="pointer-events-none absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" aria-hidden="true" />
                <input required value={form.attachmentReference} onChange={(event) => setForm({ ...form, attachmentReference: event.target.value })} placeholder="رابط آمن أو رقم المستند" className="admin-input min-h-11 pr-11" />
              </span>
            </label>
          )}
          <label className="text-sm font-bold md:col-span-2">
            السبب
            <textarea required maxLength={2000} value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} className="admin-input mt-2 min-h-24" placeholder="اكتب سببًا واضحًا يساعد مديرك على اتخاذ القرار" />
          </label>
        </div>
        <button disabled={saving || types.length === 0} className="admin-btn-primary mt-5 min-h-11">
          <Send className="h-4 w-4" aria-hidden="true" />
          {saving ? 'جارٍ الإرسال…' : 'إرسال الطلب'}
        </button>
      </form>

      <section aria-labelledby="leave-requests-heading">
        <div className="mb-3 flex items-end justify-between gap-3">
          <div>
            <h2 id="leave-requests-heading" className="text-lg font-black">طلباتي</h2>
            <p className="mt-1 text-sm text-[var(--admin-muted)]">آخر حالة مسجلة في مسار الموافقات</p>
          </div>
          <span className="hr-status hr-status--neutral">{requests.length} طلب</span>
        </div>
        <div className="space-y-3">
          {requests.length === 0 ? (
            <div className="hr-empty">
              لا توجد طلبات. استخدم النموذج أعلاه لتقديم أول طلب.
            </div>
          ) : requests.map((request) => (
            <article key={request.id} className="hr-panel">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="font-black">{request.leaveType} · {request.workdays} يوم</p>
                  <p className="mt-1 text-sm text-[var(--admin-muted)]">{request.startDate} — {request.endDate}</p>
                  <p className="mt-2 max-w-2xl text-sm leading-6">{request.reason}</p>
                </div>
                <HrStatusBadge status={request.state} />
              </div>
              {request.state === 'PendingApproval' && (
                <button type="button" onClick={() => void hrService.withdrawLeaveRequest(request.id, 'سحب بواسطة الموظف').then(load)} className="admin-btn-ghost mt-4 min-h-11">
                  سحب الطلب
                </button>
              )}
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}

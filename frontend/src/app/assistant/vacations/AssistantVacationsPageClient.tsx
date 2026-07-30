'use client';

import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import {
  AlertCircle,
  Calendar,
  CheckCircle,
  Clock,
  Compass,
  FileText,
  Loader2,
  RefreshCw,
  Send,
  WalletCards,
  XCircle,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import NeumorphButton from '@/components/ui/neumorph-button';
import { hrService, LeaveBalanceDto, LeaveRequestDto, LeaveTypeDto } from '@/services/hr-service';

type LeaveForm = {
  leaveTypeId: string;
  startDate: string;
  endDate: string;
  dayFraction: number;
  reason: string;
  attachmentReference: string;
};

const INITIAL_FORM: LeaveForm = {
  leaveTypeId: '',
  startDate: '',
  endDate: '',
  dayFraction: 1,
  reason: '',
  attachmentReference: '',
};

const STATUS_COPY: Record<string, { label: string; className: string; Icon: typeof Clock }> = {
  Draft: { label: 'مسودة', className: 'bg-slate-100 text-slate-700', Icon: FileText },
  PendingApproval: { label: 'بانتظار الاعتماد', className: 'bg-amber-100 text-amber-800', Icon: Clock },
  Approved: { label: 'معتمد', className: 'bg-emerald-100 text-emerald-800', Icon: CheckCircle },
  Rejected: { label: 'مرفوض', className: 'bg-rose-100 text-rose-800', Icon: XCircle },
  Withdrawn: { label: 'تم السحب', className: 'bg-slate-100 text-slate-700', Icon: XCircle },
  Cancelled: { label: 'ملغي', className: 'bg-slate-100 text-slate-700', Icon: XCircle },
};

function formatDate(date: string) {
  return new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium' }).format(new Date(`${date}T12:00:00`));
}

function getStatus(state: string) {
  return STATUS_COPY[state] ?? { label: state, className: 'bg-slate-100 text-slate-700', Icon: AlertCircle };
}

function balanceImpact(request: LeaveRequestDto) {
  if (request.state === 'PendingApproval') return `محجوز من الرصيد: ${request.workdays} يوم`;
  if (request.state === 'Approved') return `خُصم من الرصيد: ${request.workdays} يوم`;
  if (request.state === 'Rejected' || request.state === 'Withdrawn' || request.state === 'Cancelled') return `أُعيد إلى الرصيد: ${request.workdays} يوم`;
  return `أثر الرصيد: ${request.workdays} يوم`;
}

export default function AssistantVacationsPageClient() {
  const [types, setTypes] = useState<LeaveTypeDto[]>([]);
  const [balances, setBalances] = useState<LeaveBalanceDto[]>([]);
  const [requests, setRequests] = useState<LeaveRequestDto[]>([]);
  const [form, setForm] = useState<LeaveForm>(INITIAL_FORM);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [showRequestForm, setShowRequestForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [withdrawingId, setWithdrawingId] = useState<string | null>(null);

  const selectedType = useMemo(
    () => types.find((type) => type.id === form.leaveTypeId),
    [form.leaveTypeId, types],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
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
      setLoadError('تعذر تحميل طلبات الإجازة ورصيدك. تحقق من الاتصال ثم أعد المحاولة.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function updateForm<K extends keyof LeaveForm>(key: K, value: LeaveForm[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function submitRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!form.leaveTypeId || !form.startDate || !form.endDate || !form.reason.trim()) {
      toast.error('أكمل البيانات المطلوبة قبل إرسال الطلب.');
      return;
    }
    if (form.endDate < form.startDate) {
      toast.error('تاريخ الانتهاء يجب أن يكون في تاريخ البدء أو بعده.');
      return;
    }
    if (selectedType?.requiresAttachment && !form.attachmentReference.trim()) {
      toast.error('هذا النوع من الإجازات يتطلب مرجعًا للمرفق.');
      return;
    }

    setSaving(true);
    try {
      const response = await hrService.submitLeaveRequest({
        leaveTypeId: form.leaveTypeId,
        startDate: form.startDate,
        endDate: form.endDate,
        dayFraction: form.dayFraction,
        reason: form.reason.trim(),
        attachmentReference: form.attachmentReference.trim() || null,
      });
      if (!response.success) throw new Error(response.message);
      toast.success('تم إرسال الطلب وحجز الرصيد لحين الاعتماد.');
      setForm((current) => ({ ...INITIAL_FORM, leaveTypeId: current.leaveTypeId }));
      setShowRequestForm(false);
      await load();
    } catch (error) {
      toast.error(error instanceof Error && error.message ? error.message : 'تعذر إرسال طلب الإجازة. حاول مرة أخرى.');
    } finally {
      setSaving(false);
    }
  }

  async function withdrawRequest(request: LeaveRequestDto) {
    setWithdrawingId(request.id);
    try {
      const response = await hrService.withdrawLeaveRequest(request.id, 'سحب الطلب بواسطة الموظف');
      if (!response.success) throw new Error(response.message);
      toast.success('تم سحب الطلب وإتاحة رصيده مرة أخرى.');
      await load();
    } catch (error) {
      toast.error(error instanceof Error && error.message ? error.message : 'تعذر سحب الطلب. أعد المحاولة.');
    } finally {
      setWithdrawingId(null);
    }
  }

  return (
    <NavRouteGuard routePath="/assistant/vacations">
      <AssistantPage
        activePath="/assistant/vacations"
        sectionLabel="الموارد البشرية"
        pageTitle="طلبات الإجازة"
        subtitle="تابع رصيدك وقدّم طلباتك من نظام الموارد البشرية الموحد."
        headerAccessory={
          <NeumorphButton
            onClick={() => setShowRequestForm((visible) => !visible)}
            intent="primary"
            size="sm"
            className="flex items-center gap-1 font-bold"
            aria-expanded={showRequestForm}
            aria-controls="leave-request-form"
          >
            <Calendar className="h-4 w-4" />
            <span>{showRequestForm ? 'إغلاق الطلب' : 'طلب إجازة جديد'}</span>
          </NeumorphButton>
        }
      >
        <main className="mx-auto max-w-5xl space-y-7 text-right" dir="rtl">
          <div aria-live="polite" className="sr-only">{loading ? 'جارٍ تحديث بيانات الإجازات' : ''}</div>

          {loadError ? (
            <section role="alert" className="rounded-2xl border border-rose-200 bg-rose-50 p-5 text-rose-950">
              <div className="flex items-start gap-3">
                <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" />
                <div>
                  <h2 className="font-black">تعذر تحميل بيانات الإجازة</h2>
                  <p className="mt-1 text-sm">{loadError}</p>
                  <button type="button" onClick={() => void load()} className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-xl border border-rose-300 bg-white px-4 text-sm font-black">
                    <RefreshCw className="h-4 w-4" /> إعادة المحاولة
                  </button>
                </div>
              </div>
            </section>
          ) : null}

          <section aria-labelledby="leave-balances-heading">
            <div className="mb-3 flex items-center gap-2">
              <WalletCards className="h-5 w-5 text-[var(--admin-primary)]" />
              <h2 id="leave-balances-heading" className="text-lg font-black text-[var(--admin-text)]">رصيد الإجازات حسب النوع</h2>
            </div>
            {loading && balances.length === 0 ? (
              <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">{[1, 2, 3].map((item) => <div key={item} className="h-36 animate-pulse rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)]" />)}</div>
            ) : balances.length === 0 ? (
              <div className="rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-6 text-sm font-bold text-[var(--admin-muted)]">لا يوجد رصيد إجازات مضاف إلى ملفك بعد. تواصل مع الموارد البشرية إذا كان هذا غير متوقع.</div>
            ) : (
              <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
                {balances.map((balance) => (
                  <article key={balance.id} className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
                    <p className="font-black text-[var(--admin-text)]">{balance.leaveType}</p>
                    <p className="mt-2 text-3xl font-black text-emerald-600">{balance.available}<span className="mr-1 text-sm font-bold text-[var(--admin-muted)]">يوم متاح</span></p>
                    <p className="mt-3 text-xs font-bold leading-6 text-[var(--admin-muted)]">الممنوح {balance.granted + balance.carried} · المحجوز {balance.reserved} · المستخدم {balance.used}</p>
                  </article>
                ))}
              </div>
            )}
          </section>

          {showRequestForm ? (
            <section id="leave-request-form" aria-labelledby="new-leave-request-heading" className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm sm:p-6">
              <div className="mb-5 flex items-start gap-3">
                <span className="grid h-10 w-10 place-items-center rounded-xl bg-[var(--admin-card-soft)] text-[var(--admin-primary)]"><Calendar /></span>
                <div><h2 id="new-leave-request-heading" className="text-lg font-black text-[var(--admin-text)]">طلب إجازة جديد</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">يُحجز الرصيد عند الإرسال ويُخصم فقط بعد الاعتماد النهائي.</p></div>
              </div>
              <form onSubmit={submitRequest} className="grid gap-4 md:grid-cols-2">
                <label className="text-sm font-bold text-[var(--admin-text)]">نوع الإجازة<select required value={form.leaveTypeId} onChange={(event) => { const nextType = types.find((type) => type.id === event.target.value); setForm((current) => ({ ...current, leaveTypeId: event.target.value, dayFraction: nextType?.allowsHalfDay ? current.dayFraction : 1 })); }} className="mt-2 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3"><option value="" disabled>اختر نوع الإجازة</option>{types.map((type) => <option key={type.id} value={type.id}>{type.name}{type.isPaid ? ' (مدفوعة)' : ' (غير مدفوعة)'}</option>)}</select></label>
                <label className="text-sm font-bold text-[var(--admin-text)]">مدة اليوم<select value={form.dayFraction} onChange={(event) => updateForm('dayFraction', Number(event.target.value))} disabled={!selectedType?.allowsHalfDay} className="mt-2 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 disabled:cursor-not-allowed disabled:opacity-60"><option value={1}>يوم كامل</option>{selectedType?.allowsHalfDay ? <option value={0.5}>نصف يوم</option> : null}</select></label>
                <label className="text-sm font-bold text-[var(--admin-text)]">من<input required type="date" value={form.startDate} onChange={(event) => updateForm('startDate', event.target.value)} className="mt-2 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3" /></label>
                <label className="text-sm font-bold text-[var(--admin-text)]">إلى<input required type="date" min={form.startDate || undefined} value={form.endDate} onChange={(event) => updateForm('endDate', event.target.value)} className="mt-2 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3" /></label>
                {selectedType?.requiresAttachment ? <label className="text-sm font-bold text-[var(--admin-text)] md:col-span-2">مرجع المرفق المطلوب<input required value={form.attachmentReference} onChange={(event) => updateForm('attachmentReference', event.target.value)} placeholder="رقم المستند أو رابط المرفق" className="mt-2 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3" /></label> : null}
                <label className="text-sm font-bold text-[var(--admin-text)] md:col-span-2">سبب الإجازة<textarea required value={form.reason} onChange={(event) => updateForm('reason', event.target.value)} className="mt-2 min-h-28 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3" /></label>
                <div className="md:col-span-2"><button type="submit" disabled={saving || !types.length} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 font-black text-white disabled:cursor-not-allowed disabled:opacity-60"><Send className="h-4 w-4" />{saving ? 'جارٍ إرسال الطلب…' : 'إرسال الطلب'}</button></div>
              </form>
            </section>
          ) : null}

          <section aria-labelledby="leave-requests-heading" className="space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 id="leave-requests-heading" className="text-lg font-black text-[var(--admin-text)]">طلباتي</h2>
              <NeumorphButton onClick={() => void load()} disabled={loading} intent="ghost" size="sm" className="flex items-center gap-1 text-xs"><RefreshCw className={`h-3.5 w-3.5 ${loading ? 'animate-spin' : ''}`} /> تحديث</NeumorphButton>
            </div>
            {loading && requests.length === 0 ? <div className="space-y-3">{[1, 2].map((item) => <div key={item} className="h-40 animate-pulse rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)]" />)}</div> : requests.length === 0 ? (
              <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-12 text-center text-[var(--admin-muted)]"><Compass className="mb-4 h-12 w-12 text-[var(--admin-border)]" /><h3 className="font-black text-[var(--admin-text)]">لا توجد طلبات إجازة</h3><p className="mt-1 text-sm">عند تقديم طلب جديد سيظهر هنا مع حالة المراجعة وأثره على رصيدك.</p></div>
            ) : (
              <div className="space-y-3">
                {requests.map((request) => {
                  const status = getStatus(request.state);
                  const StatusIcon = status.Icon;
                  const canWithdraw = request.state === 'PendingApproval';
                  return <article key={request.id} className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm"><div className="flex flex-wrap items-start justify-between gap-4"><div><p className="font-black text-[var(--admin-text)]">{request.leaveType} <span className="font-medium text-[var(--admin-muted)]">· {request.workdays} يوم عمل{request.dayFraction === 0.5 ? ' (نصف يوم)' : ''}</span></p><p className="mt-2 text-sm font-bold text-[var(--admin-muted)]">{formatDate(request.startDate)} — {formatDate(request.endDate)}</p><p className="mt-3 text-sm text-[var(--admin-text)]">{request.reason}</p><p className="mt-3 text-xs font-black text-[var(--admin-muted)]">{balanceImpact(request)}</p></div><span className={`inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-black ${status.className}`}><StatusIcon className="h-3.5 w-3.5" />{status.label}</span></div>{canWithdraw ? <button type="button" onClick={() => void withdrawRequest(request)} disabled={withdrawingId === request.id} className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-xl border border-rose-300 px-4 text-sm font-black text-rose-800 hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-60">{withdrawingId === request.id ? <Loader2 className="h-4 w-4 animate-spin" /> : <XCircle className="h-4 w-4" />}{withdrawingId === request.id ? 'جارٍ سحب الطلب…' : 'سحب الطلب'}</button> : null}</article>;
                })}
              </div>
            )}
          </section>
        </main>
      </AssistantPage>
    </NavRouteGuard>
  );
}

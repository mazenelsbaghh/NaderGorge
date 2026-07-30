'use client';

import { FormEvent, useCallback, useEffect, useState } from 'react';
import { Check, Loader2, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminConfirmationDialog } from '@/components/admin';
import { ApprovalInboxDto, EmployeeDto, hrService } from '@/services/hr-service';

type PendingDecision = {
  row: ApprovalInboxDto;
  approve: boolean;
};

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleDateString('ar-EG') : 'غير متاح';
}

function formatDays(value?: number | null) {
  return value === null || value === undefined ? 'غير متاح' : `${value.toLocaleString('ar-EG')} يوم عمل`;
}

function decisionConsequence(pending: PendingDecision | null) {
  if (!pending) return '';
  const { approve, row } = pending;
  const leaveSummary = row.requestType === 'leave'
    ? ` نوع الإجازة: ${row.leaveType ?? 'غير محدد'}، المدة: ${formatDays(row.workdays)}، من ${formatDate(row.startDate)} إلى ${formatDate(row.endDate)}.`
    : '';
  return approve
    ? `سيُعتمد طلب ${row.requester}.${leaveSummary} وتُستكمل إجراءات الإجازة وفق سياسة الموارد البشرية.`
    : `سيُرفض طلب ${row.requester}.${leaveSummary} سيظهر سبب الرفض في سجل طلب الإجازة.`;
}

export function ApprovalInbox() {
  const [rows, setRows] = useState<ApprovalInboxDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);
  const [delegation, setDelegation] = useState({
    delegateUserId: '',
    startsAt: '',
    endsAt: '',
    reason: '',
  });
  const [pendingDecision, setPendingDecision] = useState<PendingDecision | null>(null);
  const [isDeciding, setIsDeciding] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [inbox, employeeRows] = await Promise.all([
        hrService.listLeaveApprovalInbox(),
        hrService.listEmployees(),
      ]);
      setRows(inbox);
      setEmployees(employeeRows);
      setDelegation((old) => ({
        ...old,
        delegateUserId: old.delegateUserId || employeeRows[0]?.userId || '',
      }));
    } catch {
      toast.error('تعذر تحميل صندوق الموافقات');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function confirmDecision(reason: string) {
    if (!pendingDecision) return;

    const { row, approve } = pendingDecision;
    setIsDeciding(true);
    try {
      await hrService.decideLeaveApproval(row.approvalInstanceId, {
        approve,
        reason: approve ? 'تمت المراجعة والاعتماد' : reason,
        expectedVersion: row.instanceVersion,
      });
      toast.success(approve ? 'تم اعتماد طلب الإجازة' : 'تم رفض طلب الإجازة');
      setPendingDecision(null);
      await load();
    } catch {
      toast.error('تعذر تسجيل القرار أو تم تعديله من مستخدم آخر');
    } finally {
      setIsDeciding(false);
    }
  }

  async function delegate(event: FormEvent) {
    event.preventDefault();
    try {
      await hrService.createApprovalDelegation({
        ...delegation,
        scope: 'leave',
        startsAt: new Date(delegation.startsAt).toISOString(),
        endsAt: new Date(delegation.endsAt).toISOString(),
      });
      toast.success('تم حفظ التفويض الزمني');
      setDelegation((old) => ({ ...old, startsAt: '', endsAt: '', reason: '' }));
    } catch {
      toast.error('تعذر حفظ التفويض');
    }
  }

  if (loading) {
    return (
      <div className="admin-panel py-16 text-center">
        <Loader2 className="mx-auto h-6 w-6 animate-spin" />
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <form onSubmit={delegate} className="admin-panel">
        <h2 className="text-lg font-black">تفويض بديل</h2>
        <p className="mt-1 text-sm text-[var(--admin-muted)]">
          يعمل البديل فقط داخل المدة المحددة، مع حفظ اسم المسؤول الأصلي والبديل.
        </p>
        <div className="mt-4 grid gap-3 md:grid-cols-4">
          <label className="text-sm font-bold">
            الموظف البديل
            <select
              value={delegation.delegateUserId}
              onChange={(event) => setDelegation({ ...delegation, delegateUserId: event.target.value })}
              className="admin-input mt-2"
            >
              {employees.map((employee) => (
                <option value={employee.userId} key={employee.id}>
                  {employee.fullName}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            من
            <input
              required
              type="datetime-local"
              value={delegation.startsAt}
              onChange={(event) => setDelegation({ ...delegation, startsAt: event.target.value })}
              className="admin-input mt-2"
            />
          </label>
          <label className="text-sm font-bold">
            إلى
            <input
              required
              type="datetime-local"
              value={delegation.endsAt}
              onChange={(event) => setDelegation({ ...delegation, endsAt: event.target.value })}
              className="admin-input mt-2"
            />
          </label>
          <label className="text-sm font-bold">
            السبب
            <input
              required
              value={delegation.reason}
              onChange={(event) => setDelegation({ ...delegation, reason: event.target.value })}
              className="admin-input mt-2"
            />
          </label>
        </div>
        <button className="admin-btn-secondary mt-4 min-h-11">حفظ التفويض</button>
      </form>

      <section className="space-y-3">
        {rows.length === 0 ? (
          <div className="admin-panel py-16 text-center font-bold text-[var(--admin-muted)]">
            لا توجد موافقات تنتظر قرارك.
          </div>
        ) : (
          rows.map((row) => (
            <article key={row.id} className="admin-panel">
              <div className="flex flex-wrap justify-between gap-3">
                <div>
                  <p className="font-black">{row.requester}</p>
                  <p className="mt-1 text-sm text-[var(--admin-muted)]">
                    {row.step} · الاستحقاق {new Date(row.dueAt).toLocaleString('ar-EG')}
                  </p>
                </div>
                <span className="admin-badge">مستوى التصعيد {row.escalationLevel}</span>
              </div>
              {row.requestType === 'leave' && (
                <div className="mt-4 space-y-3 border-t border-[var(--admin-border)] pt-4">
                  <dl className="grid gap-x-5 gap-y-3 text-sm sm:grid-cols-2 xl:grid-cols-4">
                    <div>
                      <dt className="text-xs font-bold text-[var(--admin-muted)]">نوع الإجازة</dt>
                      <dd className="mt-1 font-bold text-[var(--admin-text)]">{row.leaveType ?? 'غير متاح'}</dd>
                    </div>
                    <div>
                      <dt className="text-xs font-bold text-[var(--admin-muted)]">الفترة</dt>
                      <dd className="mt-1 font-bold text-[var(--admin-text)]">{formatDate(row.startDate)} إلى {formatDate(row.endDate)}</dd>
                    </div>
                    <div>
                      <dt className="text-xs font-bold text-[var(--admin-muted)]">المدة المطلوبة</dt>
                      <dd className="mt-1 font-bold text-[var(--admin-text)]">{formatDays(row.workdays)}{row.dayFraction === 0.5 ? '، نصف يوم' : ''}</dd>
                    </div>
                    <div>
                      <dt className="text-xs font-bold text-[var(--admin-muted)]">الرصيد المتاح حاليًا</dt>
                      <dd className="mt-1 font-bold text-[var(--admin-text)]">{formatDays(row.availableLeaveBalance)}</dd>
                    </div>
                  </dl>
                  <div className="rounded-xl bg-[var(--admin-card-soft)] px-3 py-2 text-sm text-[var(--admin-text)]">
                    <span className="font-bold">سبب الطلب: </span>
                    <span className="whitespace-pre-wrap">{row.reason || 'لم يضف الموظف سببًا.'}</span>
                  </div>
                  <p className="text-xs text-[var(--admin-muted)]">الرصيد المعروض يعكس الرصيد بعد حجز أيام هذا الطلب.</p>
                </div>
              )}
              <div className="mt-5 flex gap-2">
                <button
                  type="button"
                  onClick={() => setPendingDecision({ row, approve: true })}
                  disabled={isDeciding}
                  className="admin-btn-primary inline-flex min-h-11 items-center gap-2 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <Check className="h-4 w-4" />
                  اعتماد
                </button>
                <button
                  type="button"
                  onClick={() => setPendingDecision({ row, approve: false })}
                  disabled={isDeciding}
                  className="admin-btn-secondary inline-flex min-h-11 items-center gap-2 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <X className="h-4 w-4" />
                  رفض
                </button>
              </div>
            </article>
          ))
        )}
      </section>

      <AdminConfirmationDialog
        open={pendingDecision !== null}
        onClose={() => setPendingDecision(null)}
        onConfirm={confirmDecision}
        title={pendingDecision?.approve ? 'تأكيد اعتماد طلب الإجازة' : 'تأكيد رفض طلب الإجازة'}
        consequence={decisionConsequence(pendingDecision)}
        confirmLabel={pendingDecision?.approve ? 'اعتماد الطلب' : 'رفض الطلب'}
        variant={pendingDecision?.approve ? 'primary' : 'danger'}
        reasonLabel={pendingDecision?.approve ? undefined : 'سبب الرفض'}
        reasonPlaceholder="اكتب سببًا واضحًا للموظف وللسجل الإداري"
        reasonRequired={!pendingDecision?.approve}
        isConfirming={isDeciding}
      />
    </div>
  );
}

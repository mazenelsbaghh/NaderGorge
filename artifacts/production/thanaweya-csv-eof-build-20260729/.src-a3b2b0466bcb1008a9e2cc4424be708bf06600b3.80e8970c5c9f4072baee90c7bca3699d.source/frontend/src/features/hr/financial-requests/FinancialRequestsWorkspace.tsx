'use client';

import { FormEvent, useCallback, useEffect, useState } from 'react';
import { Landmark, Loader2, Paperclip, Send } from 'lucide-react';
import toast from 'react-hot-toast';
import {
  FinancialRequestDto,
  FinancialRequestType,
  hrPayrollService,
} from '@/services/hr-payroll-service';
import { HrStatusBadge } from '@/features/hr/components/HrStatusBadge';

const requestLabels: Record<FinancialRequestType, string> = {
  Advance: 'سلفة',
  Loan: 'قرض',
  Expense: 'استرداد مصروف',
  Commission: 'عمولة',
};

const initialForm = {
  type: 'Advance' as FinancialRequestType,
  amount: 0,
  installments: 1,
  reason: '',
  attachmentReference: '',
};

export function FinancialRequestsWorkspace() {
  const [rows, setRows] = useState<FinancialRequestDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState(initialForm);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await hrPayrollService.myFinancialRequests());
    } catch {
      toast.error('تعذر تحميل الطلبات المالية');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!form.attachmentReference.trim()) {
      toast.error('أضف رقم المستند أو رابط المرفق المؤيد');
      return;
    }

    setSaving(true);
    try {
      await hrPayrollService.submitFinancialRequest({
        ...form,
        attachmentReference: form.attachmentReference.trim(),
        reason: form.reason.trim(),
      });
      toast.success('تم إرسال الطلب إلى مسار المراجعة');
      setForm({ ...initialForm, type: form.type });
      await load();
    } catch {
      toast.error('تعذر إرسال الطلب. راجع البيانات وحاول مرة أخرى.');
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="hr-loading" role="status">
        <Loader2 className="mx-auto h-6 w-6 animate-spin text-[var(--admin-accent)]" />
        <p className="mt-3">جارٍ تحميل طلباتك المالية…</p>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <form onSubmit={submit} className="hr-panel">
        <div className="flex items-start gap-3">
          <span className="hr-icon">
            <Landmark className="h-5 w-5" aria-hidden="true" />
          </span>
          <div>
            <h2 className="text-lg font-black">طلب مالي جديد</h2>
            <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
              السلفة والقرض يُخصمان حسب الجدول، والمصروف والعمولة يُضافان بعد الاعتماد.
            </p>
          </div>
        </div>

        <div className="mt-5 grid gap-4 md:grid-cols-2">
          <label className="text-sm font-bold">
            نوع الطلب
            <select
              value={form.type}
              onChange={(event) => {
                const type = event.target.value as FinancialRequestType;
                setForm({
                  ...form,
                  type,
                  installments: type === 'Expense' || type === 'Commission' ? 1 : form.installments,
                });
              }}
              className="admin-input mt-2 min-h-11"
            >
              {Object.entries(requestLabels).map(([requestType, label]) => (
                <option key={requestType} value={requestType}>{label}</option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            القيمة بالجنيه
            <input
              required
              type="number"
              min="0.01"
              step="0.01"
              inputMode="decimal"
              value={form.amount || ''}
              onChange={(event) => setForm({ ...form, amount: Number(event.target.value) })}
              className="admin-input mt-2 min-h-11"
            />
          </label>
          {(form.type === 'Advance' || form.type === 'Loan') && (
            <label className="text-sm font-bold">
              عدد الأقساط
              <input
                required
                type="number"
                min="1"
                max="60"
                inputMode="numeric"
                value={form.installments}
                onChange={(event) => setForm({ ...form, installments: Number(event.target.value) })}
                className="admin-input mt-2 min-h-11"
              />
            </label>
          )}
          <label className="text-sm font-bold">
            مرجع المرفق
            <span className="relative mt-2 block">
              <Paperclip
                className="pointer-events-none absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]"
                aria-hidden="true"
              />
              <input
                required
                value={form.attachmentReference}
                onChange={(event) => setForm({ ...form, attachmentReference: event.target.value })}
                placeholder="رقم مستند أو رابط ملف مؤمّن"
                className="admin-input min-h-11 pr-11"
              />
            </span>
          </label>
          <label className="text-sm font-bold md:col-span-2">
            سبب الطلب
            <textarea
              required
              maxLength={2000}
              value={form.reason}
              onChange={(event) => setForm({ ...form, reason: event.target.value })}
              className="admin-input mt-2 min-h-28"
              placeholder="اكتب سببًا واضحًا يساعد المراجع على اتخاذ القرار"
            />
          </label>
        </div>
        <button disabled={saving} className="admin-btn-primary mt-5 min-h-11">
          <Send className="h-4 w-4" aria-hidden="true" />
          {saving ? 'جارٍ الإرسال…' : 'إرسال للمراجعة'}
        </button>
      </form>

      <section aria-labelledby="financial-requests-heading">
        <div className="mb-3 flex items-end justify-between gap-3">
          <div>
            <h2 id="financial-requests-heading" className="text-lg font-black">طلباتي وأقساطي</h2>
            <p className="mt-1 text-sm text-[var(--admin-muted)]">آخر حالة مسجلة لكل طلب</p>
          </div>
          <span className="hr-status hr-status--neutral">{rows.length} طلب</span>
        </div>
        <div className="space-y-3">
          {rows.length === 0 ? (
            <div className="hr-empty">
              لا توجد طلبات مالية. استخدم النموذج أعلاه لتقديم أول طلب.
            </div>
          ) : rows.map((row) => (
            <article key={row.id} className="hr-panel">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <p className="font-black">
                    {requestLabels[row.type]} · {row.amount.toLocaleString('ar-EG')} ج.م
                  </p>
                  <p className="mt-2 max-w-2xl text-sm leading-6 text-[var(--admin-muted)]">
                    {row.reason}
                  </p>
                </div>
                <div className="text-start sm:text-end">
                  <HrStatusBadge status={row.state} />
                  <p className="mt-2 text-sm font-bold">
                    المتبقي: {row.outstandingBalance.toLocaleString('ar-EG')} ج.م
                  </p>
                </div>
              </div>
              {row.installments.length > 0 && (
                <div className="mt-5 grid gap-2 border-t border-[var(--admin-border)] pt-4 sm:grid-cols-2 lg:grid-cols-3">
                  {row.installments.map((installment) => (
                    <div key={installment.id} className="hr-soft-panel p-3 text-sm">
                      <div className="flex items-start justify-between gap-3">
                        <b>قسط {installment.sequence}</b>
                        <b>{installment.amount.toLocaleString('ar-EG')} ج.م</b>
                      </div>
                      <div className="mt-2 flex items-center justify-between gap-3">
                        <span className="text-xs text-[var(--admin-muted)]">{installment.dueDate}</span>
                        <HrStatusBadge status={installment.state} />
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}

'use client';

import { useEffect, useState } from 'react';
import { ChevronDown, FileText, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { hrPayrollService, PayslipDto } from '@/services/hr-payroll-service';

function money(value: number, currency: string) {
  return `${value.toLocaleString('ar-EG', { maximumFractionDigits: 2 })} ${currency}`;
}

export function PayslipWorkspace() {
  const [rows, setRows] = useState<PayslipDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    hrPayrollService
      .myPayslips()
      .then(setRows)
      .catch(() => toast.error('تعذر تحميل كشوف الرواتب'))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="hr-loading" role="status">
        <Loader2 className="mx-auto h-6 w-6 animate-spin text-[var(--admin-accent)]" />
        <p className="mt-3">جارٍ تحميل كشوف الرواتب…</p>
      </div>
    );
  }

  if (rows.length === 0) {
    return (
      <div className="hr-empty">
        <FileText className="mx-auto mb-3 h-6 w-6 text-[var(--admin-accent)]" aria-hidden="true" />
        لا توجد كشوف رواتب مصروفة حتى الآن. سيظهر أول كشف هنا بعد اعتماد الصرف.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {rows.map((row) => (
        <article key={row.id} className="hr-panel hr-panel--accent">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="flex items-center gap-3">
              <span className="hr-icon">
                <FileText className="h-5 w-5" aria-hidden="true" />
              </span>
              <div>
                <h2 className="font-black">{row.runNumber}</h2>
                <p className="mt-1 text-sm text-[var(--admin-muted)]">
                  {row.periodStart} — {row.periodEnd}
                </p>
              </div>
            </div>
            <div className="text-start sm:text-end">
              <p className="text-xs font-bold text-[var(--admin-muted)]">صافي المستحق</p>
              <p className="mt-1 text-2xl font-black text-[var(--admin-accent)]">
                {money(row.net, row.currency)}
              </p>
            </div>
          </div>

          <dl className="hr-soft-panel mt-5 grid grid-cols-1 divide-y divide-[var(--admin-border)] p-4 text-center sm:grid-cols-3 sm:divide-x sm:divide-x-reverse sm:divide-y-0">
            <div className="py-2 sm:px-3">
              <dt className="text-xs text-[var(--admin-muted)]">إجمالي الاستحقاقات</dt>
              <dd className="mt-1 font-black">{money(row.gross, row.currency)}</dd>
            </div>
            <div className="py-2 sm:px-3">
              <dt className="text-xs text-[var(--admin-muted)]">الاستقطاعات</dt>
              <dd className="mt-1 font-black">{money(row.deductions, row.currency)}</dd>
            </div>
            <div className="py-2 sm:px-3">
              <dt className="text-xs text-[var(--admin-muted)]">صافي الراتب</dt>
              <dd className="mt-1 font-black">{money(row.net, row.currency)}</dd>
            </div>
          </dl>

          <details className="group mt-4 border-t border-[var(--admin-border)] pt-2">
            <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-3 rounded-lg px-2 font-black hover:bg-[var(--admin-hover)] focus-visible:outline-2 focus-visible:outline-[var(--admin-accent)]">
              تفاصيل البنود وطريقة الحساب
              <ChevronDown className="h-4 w-4 transition-transform group-open:rotate-180" aria-hidden="true" />
            </summary>
            <ul className="mt-2 divide-y divide-[var(--admin-border)]">
              {row.lines.map((line, index) => (
                <li key={`${line.component}-${index}`} className="py-3 text-sm">
                  <div className="flex items-start justify-between gap-4">
                    <span className="font-bold">{line.component}</span>
                    <span className="font-black">{money(line.amount, row.currency)}</span>
                  </div>
                  <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">{line.explanation}</p>
                </li>
              ))}
            </ul>
          </details>
        </article>
      ))}
    </div>
  );
}

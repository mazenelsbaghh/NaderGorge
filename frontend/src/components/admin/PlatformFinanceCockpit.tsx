'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { cairoCurrentDate, cairoCurrentMonthPeriod } from '@/lib/cairo-time';
import { AdminPage } from '@/components/admin';
import platformFinanceService, { type PlatformFinanceDashboard } from '@/services/platform-finance-service';
import FinancePeriodPicker from '@/features/platform-finance/FinancePeriodPicker';
import FinanceProfitSummary, { financeMoney as money } from '@/features/platform-finance/FinanceProfitSummary';
import FinanceLedgerDetails from '@/features/platform-finance/FinanceLedgerDetails';
import { canAccessAdminRoute } from '@/packages/admin/route-permissions';
import { useAuthStore } from '@/stores/auth-store';

const balanceLinks = [
  { href: '/admin/platform-profits', label: 'أرباح المنصّة ومستحقات المدرسين' },
  { href: '/admin/platform-finance/wallets', label: 'حركة كل محفظة' },
  { href: '/admin/platform-finance/planning', label: 'تحويل بين المحافظ والخزائن' },
];

export default function PlatformFinanceCockpit() {
  const user = useAuthStore(state => state.user);
  const [period, setPeriod] = useState(() => ({ from: cairoCurrentMonthPeriod().first, to: cairoCurrentDate() }));
  const [dashboard, setDashboard] = useState<PlatformFinanceDashboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [showLedger, setShowLedger] = useState(false);

  useEffect(() => {
    let active = true;
    setDashboard(null);
    setLoading(true);
    setError('');
    void platformFinanceService.getDashboard(period.from, period.to)
      .then(result => { if (active) setDashboard(result); })
      .catch(() => { if (active) setError('تعذر تحميل الأرصدة. جرّب مرة تانية.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [period, attempt]);

  return <AdminPage activePath="/admin/platform-finance" sectionLabel="الحسابات" pageTitle="الأرصدة والمحافظ" subtitle="عندنا كام، وللطلاب والمدرسين كام.">
    <div className="mx-auto max-w-6xl space-y-6" dir="rtl">
      <FinancePeriodPicker period={period} onChange={setPeriod} loading={loading} />
      {loading ? <div role="status" className="rounded-xl bg-[var(--admin-card)] px-6 py-16 text-center text-[var(--admin-muted)]">جارٍ تحميل الأرصدة...</div> : error ? <div role="alert" className="rounded-xl border border-[var(--admin-border)] p-6"><p>{error}</p><button type="button" className="admin-btn-primary mt-4 min-h-11 px-4" onClick={() => setAttempt(value => value + 1)}>إعادة المحاولة</button></div> : dashboard && <>
        <section aria-label="الأرصدة في نهاية الفترة" className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-6">
          <h2 className="font-bold">الفلوس في المحافظ والخزائن</h2>
          <p className="mt-3 text-3xl font-bold tabular-nums">{money(dashboard.cash)}</p>
          <p className="mt-2 text-sm text-[var(--admin-muted)]">الرصيد المسجّل لحد نهاية الفترة، مش صافي الربح.</p>
          <dl className="mt-6 divide-y divide-[var(--admin-border)] border-t border-[var(--admin-border)]">
            {[
              ['أرصدة الطلاب اللي لسه ما استخدموهاش', dashboard.generalStudentLiability + dashboard.teacherStudentLiability],
              ['باقي للمدرسين', dashboard.teacherPayable],
              ['باقي للموردين', dashboard.supplierPayable],
            ].map(([label, amount]) => <div key={label} className="flex flex-wrap justify-between gap-3 py-4 text-sm"><dt>{label}</dt><dd className="font-bold tabular-nums">{money(Number(amount))}</dd></div>)}
          </dl>
          <details className="border-t border-[var(--admin-border)] text-sm">
            <summary className="cursor-pointer pt-4 font-bold">تفاصيل أرصدة الطلاب</summary>
            <p className="mt-3">رصيد عام: {money(dashboard.generalStudentLiability)}</p>
            <p className="mt-2">رصيد مخصص لمدرّس: {money(dashboard.teacherStudentLiability)}</p>
          </details>
        </section>
        <nav aria-label="تفاصيل الأرصدة" className="flex flex-wrap gap-x-6 gap-y-2 text-sm font-bold">{balanceLinks.filter(link => canAccessAdminRoute(link.href, user)).map(link => <Link key={link.href} className="inline-flex min-h-11 items-center text-[var(--admin-primary)] underline underline-offset-4" href={link.href}>{link.label}</Link>)}</nav>
        <details className="space-y-4">
          <summary className="cursor-pointer py-3 font-bold">الدخل والمصاريف في الفترة دي</summary>
          <FinanceProfitSummary dashboard={dashboard} />
        </details>
        <details open={showLedger} className="border-t border-[var(--admin-border)]" onToggle={event => setShowLedger(event.currentTarget.open)}>
          <summary className="cursor-pointer py-5 text-sm font-bold">تفاصيل محاسبية متقدمة</summary>
          {showLedger && <FinanceLedgerDetails period={period} accounts={dashboard.accounts} />}
          <div className="flex flex-wrap gap-3 pb-5">
            <Link className="admin-btn-ghost inline-flex min-h-11 items-center px-4" href="/admin/platform-finance/reports">التقارير التفصيلية</Link>
            <a className="admin-btn-ghost inline-flex min-h-11 items-center px-4" href={`/api/admin/platform-finance/exports/xlsx?from=${period.from}&to=${period.to}`}>تنزيل Excel</a>
            <a className="admin-btn-ghost inline-flex min-h-11 items-center px-4" href={`/api/admin/platform-finance/exports/pdf?from=${period.from}&to=${period.to}`}>تنزيل PDF</a>
          </div>
        </details>
      </>}
    </div>
  </AdminPage>;
}

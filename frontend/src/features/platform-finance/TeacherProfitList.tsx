import { TeacherAccountOverview } from '@/features/teacher-finance-center/TeacherAccountOverview';
import Link from 'next/link';
import { ChevronDown } from 'lucide-react';
import type { ProfitTeacherRow } from '@/services/platform-profits-service';
import { financeMoney as money } from './FinanceProfitSummary';

export default function TeacherProfitList({ rows }: { rows: ProfitTeacherRow[] }) {
  return <ul aria-label="مستحقات المدرسين" className="divide-y divide-[var(--admin-border)]">
    {rows.map(row => {
      const { period } = row;
      const needsReview = Math.abs(row.reconciliationDifference) >= 0.01;
      const balanceLabel = 'متاح لسحب جديد الآن';
      return <li key={period.teacherId}>
        <details className="group/teacher">
          <summary className="flex min-h-20 cursor-pointer list-none items-center gap-3 py-4 [&::-webkit-details-marker]:hidden">
            <div className="min-w-0 flex-1">
              <h3 className="break-words font-bold">{period.teacherName}</h3>
              {needsReview && <p className="mt-1 text-sm text-[var(--admin-muted)]">فيه فرق محتاج مراجعة</p>}
            </div>
            <div className="shrink-0 text-end">
              <span className="text-sm text-[var(--admin-muted)]">{balanceLabel}</span>
              <p className="mt-1 font-bold tabular-nums">{row.account ? money(row.account.netPayable) : 'غير متاح'}</p>
              {row.account && row.account.netBalance < 0 && <p className="mt-1 text-xs text-[var(--admin-muted)]">مطلوب منه {money(-row.account.netBalance)}</p>}
              {!!row.account?.codeAmountDue && <p className="mt-1 text-xs text-[var(--admin-muted)]">باقي عليه من الأكواد {money(row.account.codeAmountDue)}</p>}
            </div>
            <ChevronDown size={18} aria-hidden="true" className="shrink-0 text-[var(--admin-muted)] group-open/teacher:rotate-180" />
          </summary>
          <section aria-label={`حساب ${period.teacherName}`} className="space-y-4 pb-5">
            {row.account && <TeacherAccountOverview account={row.account} />}
            <Link className="admin-btn-ghost inline-flex min-h-11 items-center px-4" href={`/admin/teachers/${period.teacherId}/account`}>فتح حساب المدرّس</Link>
            <details className="border-t border-[var(--admin-border)] pt-3">
              <summary className="cursor-pointer py-2 text-sm font-bold">{needsReview ? 'مراجعة فرق الحساب' : 'تفاصيل المراجعة'}</summary>
              <dl className="mt-3 space-y-3 text-sm">
                {[
                  ['نصيبه المسجل بالدفتر في الفترة المختارة', period.teacherShare],
                  ['المسوّى في الفترة: صرف أو نصيب محتفظ به', period.paid],
                  ['رصيد الدفتر بنهاية الفترة المختارة', period.outstanding],
                  ['رصيده في الحسابات الموحّدة الآن', row.currentLedgerBalance],
                  ['رصيده في حساب المدرّس بعد المديونية الآن', row.currentAccountBalance],
                  ['الفرق بين الرصيدين', row.reconciliationDifference],
                  ['نصيبه حسب الاتفاقات القديمة في الفترة', row.historicalPeriod.teacherShare],
                  ['الرصيد بإعادة حساب المبيعات القديمة', row.currentCalculatedBalance],
                ].map(([label, amount]) => <div key={label} className="flex flex-wrap justify-between gap-2"><dt>{label}</dt><dd className="font-bold tabular-nums">{money(Number(amount))}</dd></div>)}
              </dl>
              <p className="mt-3 text-sm leading-6 text-[var(--admin-muted)]">أرصدة «الآن» ممكن تشمل حركات بعد الفترة المختارة. الأرقام القديمة للمقارنة؛ أي تصحيح لازم يتسجّل عشان يظهر في الحسابات.</p>
            </details>
          </section>
        </details>
      </li>;
    })}
  </ul>;
}

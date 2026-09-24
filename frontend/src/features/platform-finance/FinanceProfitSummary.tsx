import type { PlatformFinanceDashboard } from '@/services/platform-finance-service';

const formatter = new Intl.NumberFormat('ar-EG-u-nu-latn', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
export const financeMoney = (amount: number) => `${formatter.format(amount)} ج.م`;

export default function FinanceProfitSummary({ dashboard }: { dashboard: PlatformFinanceDashboard }) {
  const loss = dashboard.netProfit < 0;
  return <section aria-label="ملخص الأرباح" className="overflow-hidden rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
    <dl className="grid md:grid-cols-3">
      <div className="grid grid-cols-[1fr_auto] items-center gap-x-3 p-5 md:block md:p-6">
        <dt className="text-sm font-bold md:text-base">دخل المنصّة</dt>
        <dd className="text-xl font-bold tabular-nums md:mt-3 md:text-2xl">{financeMoney(dashboard.revenue - dashboard.refunds)}</dd>
        <dd className="col-span-2 mt-2 text-sm text-[var(--admin-muted)]">نصيب المنصّة بعد المرتجعات</dd>
      </div>
      <div className="grid grid-cols-[1fr_auto] items-center gap-x-3 border-t border-[var(--admin-border)] p-5 md:block md:border-t-0 md:border-s md:p-6">
        <dt className="text-sm font-bold md:text-base">مصاريف المنصّة</dt>
        <dd className="text-xl font-bold tabular-nums md:mt-3 md:text-2xl">{financeMoney(dashboard.expenses)}</dd>
        <dd className="col-span-2 mt-2 text-sm text-[var(--admin-muted)]">المصاريف المسجّلة في الفترة دي</dd>
      </div>
      <div className="grid grid-cols-[1fr_auto] items-center gap-x-3 border-t border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 md:block md:border-t-0 md:border-s md:p-6">
        <dt className="text-sm font-bold md:text-base">{loss ? 'صافي الخسارة' : 'صافي الربح'}</dt>
        <dd className={`text-2xl font-bold tabular-nums md:mt-3 md:text-3xl ${loss ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-primary)]'}`}>{financeMoney(Math.abs(dashboard.netProfit))}</dd>
        <dd className="col-span-2 mt-2 text-sm text-[var(--admin-muted)]">{loss ? 'المصاريف أكبر من الدخل' : 'اللي كسبته المنصّة بعد المصاريف'}</dd>
      </div>
    </dl>
    <details className="border-t border-[var(--admin-border)] px-5 sm:px-6">
      <summary className="cursor-pointer py-4 text-sm font-bold">الربح اتحسب إزاي؟</summary>
      <div className="space-y-2 pb-5 text-sm leading-7 text-[var(--admin-muted)]">
        <p>دخل المنصّة {financeMoney(dashboard.revenue)} − مرتجعات المنصّة {financeMoney(dashboard.refunds)} − المصاريف {financeMoney(dashboard.expenses)} = {financeMoney(dashboard.netProfit)}.</p>
        <p>نصيب المدرسين منفصل عن دخل المنصّة. دفع مستحقاتهم مش مصروف جديد، وشحن رصيد الطالب مش ربح.</p>
      </div>
    </details>
  </section>;
}

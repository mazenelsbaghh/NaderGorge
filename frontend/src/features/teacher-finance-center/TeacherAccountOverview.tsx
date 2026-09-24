import type { TeacherFinanceSummary } from './types';

export const teacherMoney = (amount: number) => `${amount.toLocaleString('ar-EG-u-nu-latn', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ج.م`;

export const incomeSourceLabels: Record<string, string> = {
  AccessCodeActivation: 'تفعيل الأكواد', AccessCodeGeneration: 'تسليم الأكواد',
  DirectPurchase: 'شراء المحتوى', PublicExamPurchase: 'شراء الامتحانات',
  SharedPackagePurchase: 'الباقات المشتركة', Refund: 'المرتجعات', Cancellation: 'الإلغاءات',
  ManualCompensation: 'التعويضات', ManualAdjustment: 'تعديلات الحساب',
};

export function TeacherAccountOverview({ account, showSources = false }: { account: TeacherFinanceSummary; showSources?: boolean }) {
  const needsReview = Math.abs(account.sourceDifference) >= 0.01 || Math.abs(account.balanceDifference) >= 0.01;
  return <div className="space-y-5">
    <p className="text-sm leading-6 text-[var(--admin-muted)]">من بداية الحساب لحد دلوقتي. الأرباح هي نصيب المدرّس بعد المرتجعات؛ شحن رصيد الطلاب مش ربح قبل شراء المحتوى.</p>
    <dl className="grid gap-5 sm:grid-cols-3">
      {[
        ['أرباحه بعد المرتجعات', account.totalEarned],
        ['استلم فعليًا', account.paid],
        ['متاح لسحب جديد', account.netPayable],
      ].map(([label, amount]) => <div key={label} className="min-w-0 border-s-2 border-[var(--admin-border)] ps-4 last:border-[var(--admin-primary)]">
        <dt className="text-sm text-[var(--admin-muted)]">{label}</dt><dd className="mt-2 break-words text-xl font-bold tabular-nums">{teacherMoney(Number(amount))}</dd>
      </div>)}
    </dl>
    {(!!account.retained || !!account.codeAmountDue || !!account.codeAmountCollected) && <section className="rounded-xl border border-[var(--admin-border)] p-4 space-y-3" aria-label="حساب دفعات الأكواد">
      <h3 className="font-bold">الأكواد اللي استلمها المدرّس</h3>
      <dl className="grid gap-3 sm:grid-cols-3">{[
        ['نصيبه المحتفظ به', account.retained ?? 0], ['دفع للمنصّة', account.codeAmountCollected ?? 0], ['الباقي عليه للمنصّة', account.codeAmountDue ?? 0],
      ].map(([label, amount]) => <div key={label}><dt className="text-sm text-[var(--admin-muted)]">{label}</dt><dd className="mt-1 font-bold">{teacherMoney(Number(amount))}</dd></div>)}</dl>
      <p className="text-sm leading-6 text-[var(--admin-muted)]">نصيبه من الدفعات دي محسوب ضمن أرباحه ومحتفظ به بالفعل، فمش متاح يسحبه تاني. الباقي عليه بيتسدد من قسم دفعات الأكواد، ولا يُخصم تلقائيًا من سحب الأرباح الأخرى.</p>
    </section>}
    <details className="rounded-xl border border-[var(--admin-border)] p-4">
      <summary className="min-h-8 cursor-pointer font-bold">المتاح للسحب اتحسب إزاي؟</summary>
      <dl className="mt-3 space-y-3 text-sm">
        {[
          ['رصيد الحساب قبل الحجز والخصم', account.available],
          ['نخصم المحجوز لطلبات السحب والتسويات', account.reserved],
          ['نخصم المديونية اللي لسه ما اتحجزتش', account.unreservedDebt],
          ['المتاح لسحب جديد', account.netPayable],
        ].map(([label, amount]) => <div key={label} className="flex flex-wrap justify-between gap-2"><dt>{label}</dt><dd className="font-bold tabular-nums">{teacherMoney(Number(amount))}</dd></div>)}
      </dl>
      <p className="mt-4 text-sm leading-6 text-[var(--admin-muted)]">المتاح = الرصيد − المحجوز − المديونية غير المحجوزة، وبحد أدنى صفر. المحجوز لسه ما اتدفعش. لو فيه مديونية داخلة في تسوية، ما بنخصمهاش مرتين.</p>
      <p className="mt-2 text-sm leading-6 text-[var(--admin-muted)]">إجمالي المديونية: {teacherMoney(account.debt)}، منها داخل تسويات: {teacherMoney(account.debtReserved)}. الرصيد بعد كل المديونية وقبل الحجز: {teacherMoney(account.netBalance)}.</p>
    </details>
    {needsReview && <p role="status" className="rounded-xl border border-amber-500/40 p-4 text-sm leading-6">فيه فرق بين الرصيد والحركات المسجلة محتاج مراجعة: فرق الأرباح عن مصادرها {teacherMoney(account.sourceDifference)}، وفرق الرصيد عن الأرباح ناقص المدفوع والمحتفظ به {teacherMoney(account.balanceDifference)}. الفرق مش ربح إضافي.</p>}
    {showSources && <section aria-label="مصادر أرباح المدرس" className="space-y-3">
      <h3 className="font-bold">الأرباح جاية منين؟</h3>
      <p className="text-sm leading-6 text-[var(--admin-muted)]">حصة المدرّس المسجلة وقت كل عملية. البنود اللي لسه تحت المراجعة مش داخلة في الأرباح.</p>
      {account.sources.length ? <dl className="divide-y divide-[var(--admin-border)]">{account.sources.map(source => <div key={source.sourceType} className="flex flex-wrap justify-between gap-3 py-3">
        <dt>{incomeSourceLabels[source.sourceType] ?? 'مصدر آخر'} <span className="text-xs text-[var(--admin-muted)]">· {source.count} حركة</span></dt>
        <dd className="font-bold tabular-nums">{teacherMoney(source.teacherShare)}</dd>
      </div>)}</dl> : <p className="py-3 text-sm text-[var(--admin-muted)]">مفيش حركات أرباح مسجلة لحد دلوقتي.</p>}
    </section>}
  </div>;
}

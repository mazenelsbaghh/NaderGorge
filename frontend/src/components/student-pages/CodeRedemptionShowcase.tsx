import { ShieldCheck } from "lucide-react";

export function CodeRedemptionShowcase() {
  return (
    <section className="relative overflow-hidden rounded-[2.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm sm:p-10">
      <div className="absolute -left-32 -top-32 h-96 w-96 rounded-full bg-[var(--admin-primary-15)] blur-[100px]" />
      <div className="absolute -bottom-32 -right-32 h-96 w-96 rounded-full bg-[var(--admin-primary-15)] blur-[100px]" />

      <div className="relative z-10 grid gap-10 lg:grid-cols-[1fr_auto] lg:items-center">
        <div className="max-w-xl">
          <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary-20)] bg-[var(--admin-primary-10)] px-3 py-1 text-xs font-bold text-[var(--admin-primary-strong)]">
            <ShieldCheck className="h-4 w-4" />
            <span>بوابة التفعيل</span>
          </div>
          <h1 className="text-3xl font-black tracking-tight text-[var(--admin-text)] sm:text-5xl">
            فعّل كودك وافتح الباقات مباشرة
          </h1>
          <p className="mt-4 text-sm font-medium leading-relaxed text-[var(--admin-muted)] sm:text-base">
            أدخل الكود، وفي حال لزم الأمر أكمل بيانات حسابك للبدء الفوري بالدراسة ومتابعة تقدمك الأكاديمي.
          </p>
        </div>

        <div className="flex shrink-0 flex-col gap-4 rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-6 shadow-inner backdrop-blur-xl lg:min-w-[400px]">
          <h3 className="mb-2 text-sm font-bold text-[var(--admin-text)]">خطوات بسيطة:</h3>
          <ol className="space-y-4 text-sm font-medium leading-relaxed text-[var(--admin-muted)]">
            <li className="flex items-start gap-4">
              <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-card-strong)] font-black text-[var(--admin-primary)] shadow-sm">1</span>
              <span className="pt-1.5">اكتب الكود كما وصلك للتحقق الفوري.</span>
            </li>
            <li className="flex items-start gap-4">
              <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-card-strong)] font-black text-[var(--admin-primary)] shadow-sm">2</span>
              <span className="pt-1.5">أكمل بياناتك الشخصية إن زُم الأمر.</span>
            </li>
            <li className="flex items-start gap-4">
              <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-card-strong)] font-black text-[var(--admin-primary)] shadow-sm">3</span>
              <span className="pt-1.5">استمتع بفتح المسارات الدراسية المرتبطة.</span>
            </li>
          </ol>
        </div>
      </div>
    </section>
  );
}

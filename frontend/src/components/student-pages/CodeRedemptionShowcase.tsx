import { BadgeCheck, KeyRound, ShieldCheck, Sparkles } from "lucide-react";

export function CodeRedemptionShowcase() {
  return (
    <section className="relative overflow-hidden rounded-[28px] border border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] p-4 shadow-[0_24px_60px_var(--admin-shadow)] sm:rounded-[32px] sm:p-6 md:rounded-[36px] md:p-9">
      {/* Ambient glow */}
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_50%,var(--admin-primary),transparent_70%)] opacity-[0.04]" />

      <div className="relative grid gap-6 lg:grid-cols-[1.2fr_0.8fr] lg:items-center">
        <div>
          <span className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-xs font-bold text-[var(--admin-primary)]">
            <Sparkles className="h-4 w-4" />
            <span>تفعيل أسرع وتجربة أوضح</span>
          </span>
          <h1 className="mt-5 text-2xl font-black text-[var(--admin-text)] sm:text-3xl md:text-5xl">
            فعّل كودك وافتح الباقات مباشرة
          </h1>
          <p className="mt-4 max-w-2xl text-sm leading-7 text-[var(--admin-muted)] sm:text-base md:text-lg">
            صفحة التفعيل الآن أوضح: إدخال الكود، فهم ما سيحدث بعد التفعيل، ومراجعة
            سريعة للخطوات المطلوبة لو الملف الشخصي ناقص.
          </p>
        </div>

        <div className="grid gap-4">
          <GuideCard
            icon={<KeyRound className="h-5 w-5" />}
            title="أدخل الكود"
            description="اكتب الكود كما هو وسيتم التحقق منه فورًا."
          />
          <GuideCard
            icon={<ShieldCheck className="h-5 w-5" />}
            title="أكمل الملف إن لزم"
            description="لو هناك بيانات ناقصة سيظهر لك النموذج مباشرة."
          />
          <GuideCard
            icon={<BadgeCheck className="h-5 w-5" />}
            title="ابدأ المحتوى"
            description="بعد النجاح ستنتقل ذهنيًا للخطوة التالية بدون لبس."
          />
        </div>
      </div>
    </section>
  );
}

function GuideCard({
  icon,
  title,
  description,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
}) {
  return (
    <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 backdrop-blur">
      <div className="flex items-start gap-4">
        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
          {icon}
        </div>
        <div>
          <h3 className="text-lg font-black text-[var(--admin-text)]">{title}</h3>
          <p className="mt-1 text-sm leading-7 text-[var(--admin-muted)]">{description}</p>
        </div>
      </div>
    </div>
  );
}

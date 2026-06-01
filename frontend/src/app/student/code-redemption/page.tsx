"use client";

import { useState } from "react";
import { CheckCircle2, Sparkles } from "lucide-react";

import { CodeActivationForm } from "@/components/forms/CodeActivationForm";
import { CodeRedemptionShowcase } from "@/components/student-pages/CodeRedemptionShowcase";

export default function CodeRedemptionPage() {
  const [recentGrants, setRecentGrants] = useState<string[]>([]);

  return (
    <div className="space-y-12 pb-10">
      <CodeRedemptionShowcase />

      <div className="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
        <section className="group relative overflow-hidden rounded-[2.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm transition-all hover:shadow-lg sm:p-10">
          <div className="absolute -left-20 -top-20 h-40 w-40 rounded-full bg-[var(--admin-primary-15)] blur-[70px] transition-all group-hover:scale-150" />
          <div className="relative z-10 mb-8">
            <h2 className="text-3xl font-black tracking-tight text-[var(--admin-text)]">
              دخّل كود التفعيل
            </h2>
            <p className="mt-3 text-sm font-medium leading-relaxed text-[var(--admin-muted)] md:text-base">
              بعد ما تدخل الكود بنجاح هيتفتحلك الباقات الخاصة بيه، أو هيطلب منك تكمّل بياناتك.
            </p>
          </div>

          <div className="relative z-10 rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-6 shadow-inner">
            <CodeActivationForm
              onSuccess={() =>
                setRecentGrants((current) => [...current, "تم تفعيل الكود بنجاح والوصول اتفتح ✅"])
              }
            />
          </div>
        </section>

        <aside className="group relative overflow-hidden rounded-[2.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm transition-all sm:p-10">
          <div className="absolute inset-0 bg-[url('/noise.svg')] opacity-[0.02] mix-blend-overlay" />
          <div className="relative z-10">
            <h2 className="text-2xl font-black tracking-tight text-[var(--admin-text)]">
              إيه اللي هيحصل بعد التفعيل؟
            </h2>
            <div className="mt-8 flex flex-col gap-5">
              <InfoCard
                title="الوصول هيتربط بحسابك"
                description="لو الكود صالح، الباقة أو الصلاحية هتتضاف على حسابك على طول."
              />
              <InfoCard
                title="تكملة البيانات لو محتاج"
                description="لو ملفك الشخصي ناقص، هيظهرلك فورم تكمّل بيه."
              />
              <InfoCard
                title="ابدأ ذاكر فوراً"
                description="بعد التفعيل تقدر ترجع لصفحة مساراتك وتبدأ على طول."
              />
            </div>
          </div>
        </aside>
      </div>

      {recentGrants.length > 0 && (
        <section className="rounded-[30px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 backdrop-blur-xl">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-success-10)] text-[var(--admin-success)]">
              <Sparkles className="h-5 w-5" />
            </div>
            <div>
              <h3 className="text-xl font-black text-[var(--admin-text)]">آخر عمليات التفعيل</h3>
              <p className="text-sm text-[var(--admin-muted)]">
                ملخص سريع للعمليات اللي نجحت في الجلسة دي.
              </p>
            </div>
          </div>

          <div className="mt-5 space-y-3">
            {recentGrants.map((msg, i) => (
              <div
                key={`${msg}-${i}`}
                className="flex items-center gap-3 rounded-[22px] border border-[var(--admin-success-20)] bg-[var(--admin-success-10)] px-4 py-3 text-sm font-semibold text-[var(--admin-success)]"
              >
                <CheckCircle2 className="h-4 w-4 shrink-0" />
                <span>{msg}</span>
              </div>
            ))}
          </div>
        </section>
      )}


    </div>
  );
}

function InfoCard({
  title,
  description,
}: {
  title: string;
  description: string;
}) {
  return (
    <div className="rounded-[24px] bg-[var(--admin-card-soft)] p-5">
      <h3 className="text-lg font-black text-[var(--admin-text)]">{title}</h3>
      <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">{description}</p>
    </div>
  );
}

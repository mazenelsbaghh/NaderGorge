"use client";

import { useState } from "react";
import { CheckCircle2, Sparkles } from "lucide-react";

import { CodeActivationForm } from "@/components/forms/CodeActivationForm";
import { CodeRedemptionShowcase } from "@/components/student-pages/CodeRedemptionShowcase";

export default function CodeRedemptionPage() {
  const [recentGrants, setRecentGrants] = useState<string[]>([]);

  return (
    <div className="space-y-8">
      <CodeRedemptionShowcase />

      <div className="grid gap-6 xl:grid-cols-[1.15fr_0.85fr]">
        <section className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl rounded-[32px] p-6 md:p-8">
          <div className="mb-6">
            <p className="text-sm font-black tracking-[0.24em] text-[var(--admin-muted)]">
              ACTIVATE ACCESS
            </p>
            <h2 className="mt-2 text-2xl font-black text-[var(--admin-text)] md:text-3xl">
              أدخل كود التفعيل
            </h2>
            <p className="mt-2 text-base leading-8 text-[var(--admin-muted)]">
              بعد إدخال الكود بنجاح سيتم فتح الباقات الخاصة به، أو طلب استكمال الملف
              الشخصي إذا كانت هناك بيانات ناقصة.
            </p>
          </div>

          <div className="rounded-[28px] bg-[var(--admin-card-soft)] p-5">
            <CodeActivationForm
              onSuccess={() =>
                setRecentGrants((current) => [...current, "تم تفعيل الكود بنجاح وفتح الوصول."])
              }
            />
          </div>
        </section>

        <aside className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl rounded-[32px] p-6 md:p-8">
          <p className="text-sm font-black tracking-[0.24em] text-[var(--admin-muted)]">
            WHAT HAPPENS NEXT
          </p>
          <h2 className="mt-2 text-2xl font-black text-[var(--admin-text)]">
            ماذا بعد التفعيل؟
          </h2>
          <div className="mt-6 space-y-4">
            <InfoCard
              title="يتم ربط الوصول بحسابك"
              description="لو الكود صالح، ستتم إضافة الباقة أو الصلاحية مباشرة إلى حسابك الحالي."
            />
            <InfoCard
              title="استكمال البيانات عند الحاجة"
              description="إن كان ملفك الشخصي غير مكتمل، سيظهر نموذج مختصر لإتمام العملية."
            />
            <InfoCard
              title="ابدأ الدراسة فورًا"
              description="بعد نجاح التفعيل يمكنك الرجوع إلى صفحة باقاتي أو لوحة الطالب والبدء فورًا."
            />
          </div>
        </aside>
      </div>

      {recentGrants.length > 0 && (
        <section className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl rounded-[30px] p-6">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-100 text-emerald-700">
              <Sparkles className="h-5 w-5" />
            </div>
            <div>
              <h3 className="text-xl font-black text-[var(--admin-text)]">آخر عمليات التفعيل</h3>
              <p className="text-sm text-[var(--admin-muted)]">
                ملخص سريع للعمليات الناجحة في الجلسة الحالية.
              </p>
            </div>
          </div>

          <div className="mt-5 space-y-3">
            {recentGrants.map((msg, i) => (
              <div
                key={`${msg}-${i}`}
                className="flex items-center gap-3 rounded-[22px] border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-700"
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

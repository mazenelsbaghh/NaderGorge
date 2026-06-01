"use client";

import { use, useEffect, useState } from "react";
import { CheckCircle2, Sparkles } from "lucide-react";

import { CodeActivationForm } from "@/components/forms/CodeActivationForm";
import { PackageCodeRedemptionShowcase } from "@/components/student-pages/PackageCodeRedemptionShowcase";
import { contentService, type PackageCodePageDto } from "@/services/content-service";

export default function PackageCodeRedemptionPage(props: { params: Promise<{ packageId: string }> }) {
  const params = use(props.params);
  const [pageData, setPageData] = useState<PackageCodePageDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [recentGrants, setRecentGrants] = useState<string[]>([]);

  useEffect(() => {
    async function load() {
      try {
        const response = await contentService.getPackageCodePage(params.packageId);
        setPageData(response.data?.data ?? null);
      } finally {
        setLoading(false);
      }
    }

    void load();
  }, [params.packageId]);

  if (loading) {
    return (
      <div className="space-y-6">
        <div className="h-52 animate-pulse rounded-[32px] bg-[var(--admin-card-strong)]" />
        <div className="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
          <div className="h-80 animate-pulse rounded-[32px] bg-[var(--admin-card-strong)]" />
          <div className="h-80 animate-pulse rounded-[32px] bg-[var(--admin-card-strong)]" />
        </div>
      </div>
    );
  }

  if (!pageData) {
    return (
      <div className="rounded-[32px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 text-center text-[var(--admin-muted)] backdrop-blur-xl">
        تعذر تحميل صفحة الكود الخاصة بهذه الباقة.
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <PackageCodeRedemptionShowcase page={pageData} />

      <div className="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
        <section className="rounded-[32px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 backdrop-blur-xl md:p-8">
          <div className="mb-6">
            <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
              {pageData.isUsingCustomProfile ? "صفحة مخصصة" : "الوضع الافتراضي"}
            </p>
            <h2 className="mt-2 text-2xl font-black text-[var(--admin-text)] md:text-3xl">
              {pageData.activationPanel.title}
            </h2>
            <p className="mt-2 text-base leading-8 text-[var(--admin-muted)]">
              {pageData.activationPanel.description}
            </p>
          </div>

          <div className="rounded-[28px] bg-[var(--admin-card-soft)] p-4 sm:p-5">
            <CodeActivationForm
              onSuccess={() =>
                setRecentGrants((current) => [
                  ...current,
                  `تم تفعيل الكود بنجاح لـ ${pageData.packageName}.`,
                ])
              }
            />
          </div>
        </section>

        <aside className="rounded-[32px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 backdrop-blur-xl md:p-8">
          <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
            {pageData.packageName}
          </p>
          <h2 className="mt-2 text-2xl font-black text-[var(--admin-text)]">
            ماذا بعد التفعيل؟
          </h2>
          <div className="mt-6 space-y-4">
            <InfoCard title={pageData.offerPanel.title} description={pageData.offerPanel.description} />
            <InfoCard title={pageData.supportPanel.title} description={pageData.supportPanel.description} />
            <InfoCard
              title="حالة الباقة"
              description={pageData.isPackageActive ? "هذه الباقة متاحة الآن للتفعيل." : "هذه الباقة غير نشطة حاليًا وستُعرض بالوضع الافتراضي فقط."}
            />
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
              <p className="text-sm text-[var(--admin-muted)]">ملخص سريع للعمليات الناجحة في الجلسة الحالية.</p>
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

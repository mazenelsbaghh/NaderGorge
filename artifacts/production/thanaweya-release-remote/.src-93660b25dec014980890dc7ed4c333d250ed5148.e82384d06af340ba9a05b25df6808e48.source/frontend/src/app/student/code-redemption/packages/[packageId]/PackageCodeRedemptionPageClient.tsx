'use client';

import { useEffect, useState } from 'react';
import { CheckCircle2, Sparkles } from 'lucide-react';

import { CodeActivationForm } from '@/components/forms/CodeActivationForm';
import { PackageCodeRedemptionShowcase } from '@/components/student-pages/PackageCodeRedemptionShowcase';
import { contentService, type PackageCodePageDto } from '@/services/content-service';

export default function PackageCodeRedemptionPageClient({ params }: { params: { packageId: string } }) {
  const [codePage, setCodePage] = useState<PackageCodePageDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [recentActivations, setRecentActivations] = useState<string[]>([]);

  useEffect(() => {
    async function loadCodePage() {
      try {
        const response = await contentService.getPackageCodePage(params.packageId);
        setCodePage(response.data?.data ?? null);
      } finally {
        setIsLoading(false);
      }
    }

    void loadCodePage();
  }, [params.packageId]);

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="h-52 animate-pulse rounded-2xl bg-[var(--admin-card-strong)]" />
        <div className="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
          <div className="h-80 animate-pulse rounded-2xl bg-[var(--admin-card-strong)]" />
          <div className="h-80 animate-pulse rounded-2xl bg-[var(--admin-card-strong)]" />
        </div>
      </div>
    );
  }

  if (!codePage) {
    return (
      <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 text-center text-[var(--admin-muted)] backdrop-blur-xl">
        تعذر تحميل صفحة الكود الخاصة بهذه الباقة.
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <PackageCodeRedemptionShowcase page={codePage} />

      <div className="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
        <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 backdrop-blur-xl md:p-8">
          <div className="mb-6">
            <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
              {codePage.isUsingCustomProfile ? 'صفحة مخصصة' : 'الوضع الافتراضي'}
            </p>
            <h2 className="mt-2 text-2xl font-black text-[var(--admin-text)] md:text-3xl">
              {codePage.activationPanel.title}
            </h2>
            <p className="mt-2 text-base leading-8 text-[var(--admin-muted)]">
              {codePage.activationPanel.description}
            </p>
          </div>

          <div className="rounded-[28px] bg-[var(--admin-card-soft)] p-4 sm:p-5">
            <CodeActivationForm
              onSuccess={() =>
                setRecentActivations((current) => [
                  ...current,
                  `تم تفعيل الكود بنجاح لـ ${codePage.packageName}.`,
                ])
              }
            />
          </div>
        </section>

        <aside className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 backdrop-blur-xl md:p-8">
          <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
            {codePage.packageName}
          </p>
          <h2 className="mt-2 text-2xl font-black text-[var(--admin-text)]">ماذا بعد التفعيل؟</h2>
          <div className="mt-6 space-y-4">
            <InfoCard title={codePage.offerPanel.title} description={codePage.offerPanel.description} />
            <InfoCard title={codePage.supportPanel.title} description={codePage.supportPanel.description} />
            <InfoCard
              title="حالة الباقة"
              description={codePage.isPackageActive ? 'هذه الباقة متاحة الآن للتفعيل.' : 'هذه الباقة غير نشطة حاليًا وستُعرض بالوضع الافتراضي فقط.'}
            />
          </div>
        </aside>
      </div>

      {recentActivations.length > 0 && (
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
            {recentActivations.map((message, index) => (
              <div
                key={`${message}-${index}`}
                className="flex items-center gap-3 rounded-[22px] border border-[var(--admin-success-20)] bg-[var(--admin-success-10)] px-4 py-3 text-sm font-semibold text-[var(--admin-success)]"
              >
                <CheckCircle2 className="h-4 w-4 shrink-0" />
                <span>{message}</span>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

function InfoCard({ title, description }: { title: string; description: string }) {
  return (
    <div className="rounded-[24px] bg-[var(--admin-card-soft)] p-5">
      <h3 className="text-lg font-black text-[var(--admin-text)]">{title}</h3>
      <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">{description}</p>
    </div>
  );
}

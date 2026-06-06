import { LayoutGrid, PackageOpen } from "lucide-react";
import Image from "next/image";

import type { PackageDto } from "@/services/content-service";

type PackagesOverviewProps = {
  packages: PackageDto[];
};

export function PackagesOverview({ packages }: PackagesOverviewProps) {
  const enrolled = packages.filter((pkg) => pkg.isEnrolled);
  const available = packages.length - enrolled.length;

  return (
    <section className="relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm sm:p-10">
      <div className="absolute -left-32 -top-32 h-96 w-96 rounded-full bg-[var(--admin-primary-15)] blur-[48px]" />
      
      <div className="relative z-10 flex flex-col gap-10 lg:flex-row lg:items-end lg:justify-between">
        <div className="max-w-xl">
          <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary-20)] bg-[var(--admin-primary-10)] px-3 py-1 text-xs font-bold text-[var(--admin-primary-strong)]">
            <LayoutGrid className="h-3.5 w-3.5" />
            <span>مكتبتك الشاملة</span>
          </div>
          <h1 className="text-3xl font-black tracking-tight text-[var(--admin-text)] sm:text-5xl">
            الباقات والمسارات
          </h1>
          <p className="mt-4 text-sm font-medium leading-relaxed text-[var(--admin-muted)] sm:text-base">
            تصفح مساراتك التعليمية الحالية أو اكتشف المزيد من الباقات المتوفرة لتعزيز رحلتك الأكاديمية معنا.
          </p>
        </div>

        <div className="flex shrink-0 items-center gap-6 rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 shadow-inner backdrop-blur-xl sm:p-6 lg:min-w-[400px]">
          <Metric label="الإجمالي" value={packages.length} />
          <div className="h-10 w-px bg-[var(--admin-border)]" />
          <Metric label="مفعّلة" value={enrolled.length} active />
          <div className="h-10 w-px bg-[var(--admin-border)]" />
          <Metric label="تحتاج تفعيل" value={available} />
        </div>
      </div>
    </section>
  );
}

function Metric({ label, value, active }: { label: string; value: number; active?: boolean }) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center text-center">
      <dt className="mb-1 text-[10px] font-bold uppercase tracking-wider text-[var(--admin-muted)]">
        {label}
      </dt>
      <dd className={`text-3xl font-black ${active ? "text-[var(--admin-primary)] drop-shadow-md" : "text-[var(--admin-text)]"}`}>
        {value}
      </dd>
    </div>
  );
}

export function PackagesGrid({
  title,
  description,
  packages,
  actionLabel,
  emptyTitle,
  emptyDescription,
  onAction,
}: {
  title: string;
  description: string;
  packages: PackageDto[];
  actionLabel: string;
  emptyTitle: string;
  emptyDescription: string;
  onAction: (packageId: string) => void;
}) {
  return (
    <section className="space-y-6">
      <div className="border-b border-[var(--admin-border)] pb-4">
        <h2 className="text-2xl font-black text-[var(--admin-text)]">{title}</h2>
        <p className="mt-1 text-sm font-medium text-[var(--admin-muted)]">{description}</p>
      </div>

      {packages.length === 0 ? (
        <div className="mx-auto max-w-3xl">
          <div className="group relative flex flex-col items-center justify-center overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[linear-gradient(to_bottom,var(--admin-card),var(--admin-card-soft))] p-10 text-center shadow-sm">
            <div className="absolute inset-0 bg-[url('/noise.svg')] opacity-[0.03] mix-blend-overlay" />
            <div className="relative mb-6 flex h-24 w-24 items-center justify-center rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-xl transition-transform duration-500 group-hover:-translate-y-2 group-hover:rotate-3">
              <PackageOpen className="h-10 w-10 text-[var(--admin-primary)] opacity-80" />
              <div className="absolute inset-0 rounded-[2rem] shadow-[inset_0_0_20px_var(--admin-primary-15)]" />
            </div>
            <h3 className="relative text-xl font-bold text-[var(--admin-text)]">{emptyTitle}</h3>
            <p className="relative mt-2 max-w-sm text-sm leading-relaxed text-[var(--admin-muted)]">
              {emptyDescription}
            </p>
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {packages.map((pkg) => (
            <button
              type="button"
              key={pkg.id}
              onClick={() => onAction(pkg.id)}
              className="group relative flex cursor-pointer flex-col overflow-hidden rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] text-right shadow-sm transition-all hover:-translate-y-1 hover:shadow-xl hover:shadow-[var(--admin-primary-10)] hover:border-[var(--admin-primary-30)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
            >
              {/* Image Frame */}
              <div className="relative aspect-[4/3] w-full overflow-hidden bg-[var(--admin-bg)]">
                <Image
                  src={pkg.imageUrl || '/images/default-package.png'}
                  alt={pkg.name}
                  fill
                  className="object-cover transition-transform duration-700 ease-out group-hover:scale-[1.03]"
                  sizes="(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-[var(--admin-card)] to-transparent via-[var(--admin-card)]/20" />
                
                {/* Badge */}
                <div className="absolute left-4 top-4">
                  <span
                    className={`inline-flex items-center rounded-lg px-2.5 py-1 text-[11px] font-black uppercase tracking-wider shadow-md backdrop-blur-md ${
                      pkg.isEnrolled
                        ? "bg-[var(--admin-success-20)] text-[var(--admin-success)] border border-[var(--admin-success-30)]"
                        : "bg-[var(--admin-card-strong)] text-[var(--admin-text)] border border-[var(--admin-border)]"
                    }`}
                  >
                    {pkg.isEnrolled ? "مفعّلة" : "مغلقة"}
                  </span>
                </div>
              </div>

              {/* Content Body */}
              <div className="relative z-10 flex flex-1 flex-col p-5 pt-0">
                <div className="mb-4 flex items-end justify-between gap-3 border-b border-[var(--admin-border)] pb-4">
                  <h3 className="line-clamp-2 text-lg font-black leading-snug text-[var(--admin-text)] transition-colors group-hover:text-[var(--admin-primary)]">
                    {pkg.name}
                  </h3>
                </div>
                
                <p className="mb-6 line-clamp-2 text-xs font-medium leading-relaxed text-[var(--admin-muted)]">
                  {pkg.description || 'باقة تعليمية متكاملة لضمان التفوق الأكاديمي. تتضمن كافة الشروحات الضرورية.'}
                </p>

                <div className="mt-auto flex items-center justify-between">
                  <div className="flex flex-col">
                    <span className="text-[10px] font-bold text-[var(--admin-muted)]">السعر</span>
                    <span className="font-mono text-lg font-black text-[var(--admin-text)]">
                      {pkg.price.toFixed(0)} <span className="text-[10px] font-sans">ج.م</span>
                    </span>
                  </div>
                  
                  <div className={`flex h-10 items-center justify-center rounded-xl border px-5 text-xs font-bold transition-all ${
                    pkg.isEnrolled 
                      ? "border-transparent bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary-20)] group-hover:bg-[var(--admin-primary-strong)]"
                      : "border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-text)] group-hover:border-[var(--admin-text)] group-hover:bg-[var(--admin-text)] group-hover:text-[var(--admin-bg)]"
                  }`}>
                    {actionLabel}
                  </div>
                </div>
              </div>
            </button>
          ))}
        </div>
      )}
    </section>
  );
}

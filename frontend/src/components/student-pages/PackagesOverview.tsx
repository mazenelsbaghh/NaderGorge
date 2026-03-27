import { BookCopy, CheckCircle2, Layers3, LockKeyhole } from "lucide-react";
import Image from "next/image";
import Link from "next/link";

import type { PackageDto } from "@/services/content-service";

type PackagesOverviewProps = {
  packages: PackageDto[];
};

export function PackagesOverview({ packages }: PackagesOverviewProps) {
  const enrolled = packages.filter((pkg) => pkg.isEnrolled);
  const available = packages.length - enrolled.length;

  return (
    <section className="relative overflow-hidden rounded-[28px] border border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] p-4 shadow-[0_24px_60px_var(--admin-shadow)] sm:rounded-[32px] sm:p-6 md:rounded-[36px] md:p-9">
      <div className="relative grid gap-5 md:grid-cols-3">
        <div className="md:col-span-2">
          <span className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-xs font-bold text-[var(--admin-primary)]">YOUR LIBRARY</span>
          <h1 className="mt-5 text-2xl font-black text-[var(--admin-text)] sm:text-3xl md:text-5xl">
            باقاتك ومساراتك الدراسية
          </h1>
          <p className="mt-4 max-w-2xl text-sm leading-7 text-[var(--admin-muted)] sm:text-base md:text-lg">
            هنا ستجد كل الباقات المتاحة لك بشكل أوضح: الباقات المفعّلة، الباقات التي
            تحتاج كود، ونظرة سريعة على حجم مكتبتك التعليمية.
          </p>
        </div>

        <div className="grid gap-4">
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 backdrop-blur">
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="text-sm font-bold text-[var(--admin-muted)]">إجمالي الباقات</p>
                <p className="mt-2 text-3xl font-black text-[var(--admin-text)]">
                  {packages.length}
                </p>
              </div>
              <Layers3 className="h-7 w-7 text-[var(--admin-primary)]" />
            </div>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 md:grid-cols-1">
            <InfoStat
              label="مفعّلة"
              value={enrolled.length}
              icon={<CheckCircle2 className="h-5 w-5" />}
            />
            <InfoStat
              label="تحتاج تفعيل"
              value={available}
              icon={<LockKeyhole className="h-5 w-5" />}
            />
          </div>
        </div>
      </div>
    </section>
  );
}

function InfoStat({
  label,
  value,
  icon,
}: {
  label: string;
  value: number;
  icon: React.ReactNode;
}) {
  return (
    <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 backdrop-blur">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-sm font-bold text-[var(--admin-muted)]">{label}</p>
          <p className="mt-1 text-2xl font-black text-[var(--admin-text)]">{value}</p>
        </div>
        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
          {icon}
        </div>
      </div>
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
      <div className="flex flex-col gap-2">
        <h2 className="text-3xl font-black text-[var(--admin-text)] leading-tight">{title}</h2>
        <p className="max-w-3xl text-base leading-relaxed text-[var(--admin-muted)]">{description}</p>
      </div>

      {packages.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-[32px] border-2 border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-12 text-center">
          <div className="mb-6 flex h-20 w-20 items-center justify-center rounded-full bg-[var(--admin-card)] shadow-sm text-[var(--admin-primary)] border border-[var(--admin-border)]">
            <BookCopy className="h-10 w-10" />
          </div>
          <h3 className="text-2xl font-bold text-[var(--admin-text)]">{emptyTitle}</h3>
          <p className="mt-3 max-w-lg text-base leading-relaxed text-[var(--admin-muted)]">
            {emptyDescription}
          </p>
        </div>
      ) : (
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {packages.map((pkg) => (
            <Link
              key={pkg.id}
              href={`/student/packages/${pkg.id}`}
              className="group relative flex flex-col overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm transition-all hover:shadow-xl hover:-translate-y-1 cursor-pointer"
            >
              {/* Image Header */}
              <div className="relative aspect-[16/9] w-full overflow-hidden bg-[var(--admin-card-strong)] border-b border-[var(--admin-border)]">
                <Image 
                  src={pkg.imageUrl || '/images/default-package.png'} 
                  alt={pkg.name} 
                  fill
                  className="object-cover transition-transform duration-700 group-hover:scale-105"
                  sizes="(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw"
                />
                
                {/* Status Badge */}
                <div className="absolute top-4 right-4">
                  <span
                    className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-bold shadow-sm backdrop-blur-md border ${
                      pkg.isEnrolled
                        ? "bg-emerald-500/20 text-emerald-300 border-emerald-500/30"
                        : "bg-red-500/20 text-red-300 border-red-500/30"
                    }`}
                  >
                    {pkg.isEnrolled ? "مفعّلة" : "تحتاج كود"}
                  </span>
                </div>
              </div>

              {/* Body */}
              <div className="flex flex-col flex-grow p-6">
                <div className="mb-3 flex items-start justify-between gap-4">
                  <h3 className="text-xl font-bold text-[var(--admin-text)] leading-tight line-clamp-2">
                    {pkg.name}
                  </h3>
                  <span className="shrink-0 rounded-xl bg-[var(--admin-card-strong)] px-3 py-1 text-lg font-black text-[var(--admin-primary)] border border-[var(--admin-border)]">
                    {pkg.price.toFixed(0)} LE
                  </span>
                </div>
                
                <p className="line-clamp-2 text-sm text-[var(--admin-muted)] leading-relaxed mb-6">
                  {pkg.description || 'باقة تعليمية متكاملة لضمان التفوق الأكاديمي. تتضمن الشرح والتدريبات اللازمة لاجتياز الاختبارات بامتياز.'}
                </p>

                {/* Footer CTA */}
                <div className="mt-auto pt-4 border-t border-[var(--admin-border)]">
                  <span
                    className={`w-full rounded-xl px-4 py-3 text-sm font-bold transition-colors flex items-center justify-center gap-2 ${
                      pkg.isEnrolled
                        ? "bg-[var(--admin-card-strong)] text-[var(--admin-primary)] group-hover:bg-[var(--admin-primary)] group-hover:text-[var(--admin-primary-contrast)]"
                        : "bg-[var(--admin-card-soft)] text-[var(--admin-text)] border border-[var(--admin-border)] group-hover:bg-[var(--admin-card-strong)]"
                    }`}
                  >
                    {actionLabel}
                  </span>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}

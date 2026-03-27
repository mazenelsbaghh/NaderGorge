"use client";

import { ArrowUpLeft, BookCopy } from "lucide-react";
import Image from "next/image";
import Link from "next/link";

import type { ActivePackageDto } from "@/services/student-service";
import { useViewTransition } from "@/lib/use-view-transition";

type PackageGridProps = {
  packages: ActivePackageDto[];
  onOpenPackage: (packageId: string) => void;
  onActivateCode: () => void;
};

export function PackageGrid({
  packages,
  onOpenPackage,
  onActivateCode,
}: PackageGridProps) {
  const navigateWithTransition = useViewTransition();
  return (
    <section className="space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-sm font-black tracking-[0.24em] text-[var(--admin-muted)]">
            YOUR PACKAGES
          </p>
          <h2 className="mt-2 text-2xl font-black text-[var(--admin-text)]">
            الباقات الدراسية
          </h2>
        </div>
      </div>

      {packages.length === 0 ? (
        <div className="rounded-[32px] border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] p-10 text-center backdrop-blur-xl">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-3xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
            <BookCopy className="h-7 w-7" />
          </div>
          <h3 className="mt-5 text-2xl font-black text-[var(--admin-text)]">
            لا توجد باقات مفعلة
          </h3>
          <p className="mx-auto mt-3 max-w-xl text-base leading-8 text-[var(--admin-muted)]">
            فعّل كودك أولًا، وبعدها هتظهر الباقات هنا بشكل مرتب مع التقدم وآخر نقطة
            وصلت لها.
          </p>
          <button
            onClick={onActivateCode}
            className="mt-6 inline-flex items-center justify-center rounded-full bg-[var(--admin-primary)] px-6 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] transition hover:-translate-y-0.5 hover:bg-[var(--admin-primary-strong)]"
          >
            تفعيل كود
          </button>
        </div>
      ) : (
        <div className="grid gap-5 xl:grid-cols-2">
          {packages.map((pkg) => (
            <div
              key={pkg.id}
              onClick={() => navigateWithTransition(`/student/packages/${pkg.id}`)}
              className="group relative flex flex-col overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm transition-all hover:shadow-xl hover:-translate-y-1 cursor-pointer"
            >
              {/* Image Header */}
              <div
                className="relative aspect-[21/9] w-full overflow-hidden bg-[var(--admin-card-strong)] border-b border-[var(--admin-border)]"
                style={{ viewTransitionName: `pkg-image-${pkg.id}` }}
              >
                <Image 
                  src={pkg.imageUrl || '/images/default-package.png'} 
                  alt={pkg.name} 
                  fill
                  className="object-cover transition-transform duration-700 group-hover:scale-105"
                  sizes="(max-width: 768px) 100vw, 50vw"
                />
              </div>

              {/* Body */}
              <div className="flex flex-col flex-grow p-6">
                <div className="mb-2 flex items-start justify-between gap-4">
                  <h3
                    className="text-xl font-bold text-[var(--admin-text)] leading-tight line-clamp-1"
                    style={{ viewTransitionName: `pkg-title-${pkg.id}` }}
                  >
                    {pkg.name}
                  </h3>
                  <div className="shrink-0 flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)] border border-[var(--admin-border)]">
                    <BookCopy className="h-5 w-5" />
                  </div>
                </div>
                
                <p className="line-clamp-2 text-sm text-[var(--admin-muted)] leading-relaxed mb-6">
                  {pkg.description || 'باقة تعليمية متكاملة لضمان التفوق الأكاديمي. تتضمن الشرح والتدريبات.'}
                </p>

                {/* Progress & Footer CTA */}
                <div className="mt-auto grid gap-4 sm:grid-cols-[1fr_auto] sm:items-end pt-4 border-t border-[var(--admin-border)]">
                  <div className="w-full">
                    <div className="mb-2 flex items-center justify-between text-sm">
                      <span className="font-bold text-[var(--admin-muted)]">
                        الدروس: {pkg.lessonsCompleted}/{pkg.totalLessons}
                      </span>
                      <span className="font-black text-[var(--admin-primary)]">
                        {pkg.progressPercent}%
                      </span>
                    </div>
                    <div className="h-2.5 w-full rounded-full bg-[var(--admin-card-strong)] overflow-hidden">
                      <div
                        className="h-full rounded-full bg-[var(--admin-primary)]"
                        style={{ width: `${pkg.progressPercent}%`, transition: 'width 1s ease-in-out' }}
                      />
                    </div>
                  </div>

                  <span
                    className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-card-strong)] px-5 text-sm font-bold text-[var(--admin-primary)] transition-colors group-hover:bg-[var(--admin-primary)] group-hover:text-[var(--admin-primary-contrast)] shrink-0"
                  >
                    افتح الباقة
                    <ArrowUpLeft className="h-4 w-4" />
                  </span>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

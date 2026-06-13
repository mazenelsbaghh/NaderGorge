"use client";

import { ArrowUpLeft, BookCopy } from "lucide-react";
import Image from "next/image";

import type { ActivePackageDto } from "@/services/student-service";
import { resolveMediaUrl } from "@/utils/resolve-media-url";

type PackageGridProps = {
  packages: ActivePackageDto[];
  onOpenPackage: (packageId: string) => void;
  onActivateCode: (packageId?: string) => void;
};

export function PackageGrid({
  packages,
  onOpenPackage,
  onActivateCode,
}: PackageGridProps) {
  const packagesBySubject = packages.reduce<Record<string, ActivePackageDto[]>>(
    (groups, pkg) => {
      const subjectName = pkg.subjectName || "عام";
      groups[subjectName] = [...(groups[subjectName] ?? []), pkg];
      return groups;
    },
    {},
  );

  return (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-black text-[var(--admin-text)]">باقاتك الدراسية</h2>
          <p className="mt-1 text-sm text-[var(--admin-muted)]">
            افتح باقة لمتابعة دروسها وتقدمك داخلها.
          </p>
        </div>
        {packages.length > 0 && (
          <span className="shrink-0 rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-bold text-[var(--admin-primary)]">
            {packages.length} باقة
          </span>
        )}
      </div>

      {packages.length === 0 ? (
        <div className="mt-5 flex flex-col items-start gap-4 rounded-xl border border-dashed border-[var(--admin-border)] p-5 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-start gap-3">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-card-soft)] text-[var(--admin-primary)]">
              <BookCopy className="h-5 w-5" />
            </div>
            <div>
              <h3 className="font-black text-[var(--admin-text)]">لا توجد باقات مفعلة</h3>
              <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
                فعّل كودًا لفتح الباقة والدروس المرتبطة بها.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={() => onActivateCode()}
            className="inline-flex min-h-11 w-full items-center justify-center rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] transition-colors hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] sm:w-auto"
          >
            فعّل كودًا
          </button>
        </div>
      ) : (
        <div className="mt-6 space-y-6">
          {Object.entries(packagesBySubject).map(([subject, subjectPackages]) => (
            <div key={subject}>
              <h3 className="mb-3 text-sm font-black text-[var(--admin-text)]">{subject}</h3>
              <div className="divide-y divide-[var(--admin-border)] overflow-hidden rounded-xl border border-[var(--admin-border)]">
                {subjectPackages.map((pkg) => {
                  const progress = Math.max(0, Math.min(pkg.progressPercent, 100));

                  return (
                    <button
                      type="button"
                      key={pkg.id}
                      onClick={() => onOpenPackage(pkg.id)}
                      className="group flex min-h-[104px] w-full items-center gap-3 bg-[var(--admin-card)] p-3 text-right transition-colors hover:bg-[var(--admin-card-soft)] focus-visible:relative focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)] sm:gap-4"
                    >
                      <div className="relative h-20 w-24 shrink-0 overflow-hidden rounded-lg bg-[var(--admin-card-strong)] sm:w-32">
                        <Image
                          src={pkg.imageUrl ? resolveMediaUrl(pkg.imageUrl) : "/images/default-package.webp"}
                          alt=""
                          fill
                          className="object-cover"
                          sizes="(max-width: 640px) 96px, 128px"
                        />
                      </div>

                      <div className="min-w-0 flex-1">
                        <div className="flex items-start justify-between gap-3">
                          <div className="min-w-0">
                            <h4 className="truncate text-base font-black text-[var(--admin-text)]">
                              {pkg.name}
                            </h4>
                            <p className="mt-1 truncate text-xs text-[var(--admin-muted)]">
                              {pkg.teacherName}
                            </p>
                          </div>
                          <ArrowUpLeft className="mt-1 h-4 w-4 shrink-0 text-[var(--admin-primary)]" />
                        </div>

                        <div className="mt-3">
                          <div className="mb-1.5 flex items-center justify-between gap-3 text-xs">
                            <span className="font-bold text-[var(--admin-muted)]">
                              {pkg.lessonsCompleted} من {pkg.totalLessons} درس
                            </span>
                            <span className="font-black text-[var(--admin-primary)]">{progress}%</span>
                          </div>
                          <div
                            className="h-1.5 overflow-hidden rounded-full bg-[var(--admin-card-strong)]"
                            role="progressbar"
                            aria-label={`التقدم في ${pkg.name}`}
                            aria-valuemin={0}
                            aria-valuemax={100}
                            aria-valuenow={progress}
                          >
                            <div
                              className="h-full rounded-full bg-[var(--admin-primary)]"
                              style={{ width: `${progress}%` }}
                            />
                          </div>
                        </div>
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

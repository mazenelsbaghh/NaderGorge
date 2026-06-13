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
  // Group packages by Subject
  const packagesBySubject = packages.reduce((acc, pkg) => {
    const subjectName = pkg.subjectName || "عام";
    if (!acc[subjectName]) {
      acc[subjectName] = [];
    }
    acc[subjectName].push(pkg);
    return acc;
  }, {} as Record<string, ActivePackageDto[]>);

  return (
    <section className="space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
            باقاتك
          </p>
          <h2 className="mt-2 text-2xl font-black text-[var(--admin-text)]">
            الباقات الدراسية
          </h2>
        </div>
      </div>

      {packages.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-10 text-center backdrop-blur-xl">
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
            onClick={() => onActivateCode()}
            className="mt-6 inline-flex items-center justify-center rounded-full bg-[var(--admin-primary)] px-6 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] transition hover:-translate-y-0.5 hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
          >
            تفعيل كود
          </button>
        </div>
      ) : (
        <div className="space-y-10">
          {Object.entries(packagesBySubject).map(([subject, list]) => (
            <div key={subject} className="space-y-4">
              <h3 className="text-lg font-black text-[var(--admin-primary)] border-r-4 border-[var(--admin-primary)] pr-3 select-none">
                {subject}
              </h3>
              
              <div className="grid gap-5 xl:grid-cols-2">
                {list.map((pkg) => (
                  <button
                    type="button"
                    key={pkg.id}
                    onClick={() => onOpenPackage(pkg.id)}
                    className="group relative flex flex-col overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] text-right shadow-sm transition-all hover:shadow-xl hover:-translate-y-1 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)] cursor-pointer"
                  >
                    {/* Image Header */}
                    <div
                      className="relative aspect-video w-full overflow-hidden bg-[var(--admin-card-strong)] border-b border-[var(--admin-border)]"
                      style={{ viewTransitionName: `pkg-image-${pkg.id}` }}
                    >
                      <Image 
                        src={pkg.imageUrl ? resolveMediaUrl(pkg.imageUrl) : '/images/default-package.webp'}
                        alt={pkg.name} 
                        fill
                        className="object-cover transition-transform duration-700 group-hover:scale-105"
                        sizes="(max-width: 768px) 100vw, 50vw"
                      />
                      
                      {/* Teacher Profile Overlay */}
                      <div className="absolute bottom-3 right-3 flex items-center gap-2 rounded-full bg-black/60 backdrop-blur-md px-3 py-1.5 border border-white/10 select-none">
                        <div className="relative h-6 w-6 overflow-hidden rounded-full border border-white/20">
                          <Image
                            src={pkg.teacherProfileImageUrl || `https://avatar.vercel.sh/${encodeURIComponent(pkg.teacherName)}`}
                            alt={pkg.teacherName}
                            fill
                            className="object-cover"
                            sizes="24px"
                            unoptimized
                          />
                        </div>
                        <span className="text-[10px] font-black text-white">{pkg.teacherName}</span>
                      </div>
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
                              className="h-full origin-right rounded-full bg-[var(--admin-primary)] transition-transform duration-700 ease-out transform-gpu"
                              style={{ transform: `scaleX(${Math.max(0, Math.min(pkg.progressPercent, 100)) / 100})` }}
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
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

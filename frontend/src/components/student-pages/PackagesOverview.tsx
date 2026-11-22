import { LayoutGrid, PackageOpen } from "lucide-react";
import { useState, useMemo } from "react";
import Image from "next/image";
import Link from "next/link";

import type { PackageDto } from "@/services/content-service";
import { resolveMediaUrl } from "@/utils/resolve-media-url";

type PackagesOverviewProps = {
  packages: PackageDto[];
};

export function PackagesOverview({ packages }: PackagesOverviewProps) {
  const enrolled = packages.filter((pkg) => pkg.isEnrolled);
  const available = packages.length - enrolled.length;

  return (
    <section className="relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm sm:p-10">
      <div className="absolute -left-32 -top-32 h-96 w-96 rounded-full bg-[var(--admin-primary-15)] blur-3xl" />
      
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

        <div className="flex shrink-0 items-center gap-6 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 shadow-inner sm:p-6 lg:min-w-[400px]">
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
      <dt className="mb-1 text-xs font-bold uppercase tracking-wider text-[var(--admin-muted)]">
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
  getHref,
}: {
  title: string;
  description: string;
  packages: PackageDto[];
  actionLabel: string;
  emptyTitle: string;
  emptyDescription: string;
  getHref: (packageId: string) => string;
}) {
  const [selectedTeacher, setSelectedTeacher] = useState<string | null>(null);
  const [selectedSubject, setSelectedSubject] = useState<string | null>(null);

  const teachers = useMemo(() => {
    const map = new Map<string, { id: string; name: string }>();
    packages.forEach((pkg) => {
      if (pkg.teacherId && pkg.teacherName) {
        map.set(pkg.teacherId, {
          id: pkg.teacherId,
          name: pkg.teacherName,
        });
      }
    });
    return Array.from(map.values());
  }, [packages]);

  const subjects = useMemo(() => {
    const map = new Map<string, { id: string; name: string }>();
    packages.forEach((pkg) => {
      if (pkg.subjectId && pkg.subjectName) {
        map.set(pkg.subjectId, { id: pkg.subjectId, name: pkg.subjectName });
      }
    });
    return Array.from(map.values());
  }, [packages]);

  const filteredPackages = useMemo(() => {
    return packages.filter((pkg) => {
      if (selectedTeacher && pkg.teacherId !== selectedTeacher) return false;
      if (selectedSubject && pkg.subjectId !== selectedSubject) return false;
      return true;
    });
  }, [packages, selectedTeacher, selectedSubject]);

  const hasFilters = teachers.length > 1 || subjects.length > 1;

  return (
    <section className="space-y-6">
      <div className="border-b border-[var(--admin-border)] pb-4">
        <h2 className="text-2xl font-black text-[var(--admin-text)]">{title}</h2>
        <p className="mt-1 text-sm font-medium text-[var(--admin-muted)]">{description}</p>
      </div>

      {hasFilters && packages.length > 0 && (
        <fieldset className="grid min-w-0 gap-3 sm:grid-cols-2 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto]" aria-label="تصفية الباقات">
          {teachers.length > 1 && (
            <label className="min-w-0 text-sm font-bold text-[var(--admin-text)]">
              المدرس
              <select value={selectedTeacher ?? ''} onChange={event => setSelectedTeacher(event.target.value || null)}
                className="mt-2 block min-h-11 w-full min-w-0 max-w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)]">
                <option value="">كل المدرسين</option>
                {teachers.map(teacher => <option key={teacher.id} value={teacher.id}>{teacher.name}</option>)}
              </select>
            </label>
          )}
          {subjects.length > 1 && (
            <label className="min-w-0 text-sm font-bold text-[var(--admin-text)]">
              المادة
              <select value={selectedSubject ?? ''} onChange={event => setSelectedSubject(event.target.value || null)}
                className="mt-2 block min-h-11 w-full min-w-0 max-w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)]">
                <option value="">كل المواد</option>
                {subjects.map(subject => <option key={subject.id} value={subject.id}>{subject.name}</option>)}
              </select>
            </label>
          )}
          {(selectedTeacher || selectedSubject) && (
            <button type="button" onClick={() => { setSelectedTeacher(null); setSelectedSubject(null); }}
              className="min-h-11 self-end rounded-xl px-4 text-sm font-bold text-[var(--admin-primary)] underline focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)]">
              مسح التصفية
            </button>
          )}
        </fieldset>
      )}

      {filteredPackages.length === 0 ? (
        <div className="mx-auto max-w-3xl">
          <div className="group relative flex flex-col items-center justify-center overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[linear-gradient(to_bottom,var(--admin-card),var(--admin-card-soft))] p-10 text-center shadow-sm">
            <div className="absolute inset-0 bg-[url('/noise.svg')] opacity-[0.03] mix-blend-overlay" />
            <div className="relative mb-6 flex h-24 w-24 items-center justify-center rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-xl transition-transform duration-500 group-hover:-translate-y-2 group-hover:rotate-3">
              <PackageOpen className="h-10 w-10 text-[var(--admin-primary)] opacity-80" />
              <div className="absolute inset-0 rounded-2xl shadow-sm" />
            </div>
            <h3 className="relative text-xl font-bold text-[var(--admin-text)]">
              {(selectedTeacher || selectedSubject) ? "لا توجد باقات تطابق الفلتر" : emptyTitle}
            </h3>
            <p className="relative mt-2 max-w-sm text-sm leading-relaxed text-[var(--admin-muted)]">
              {(selectedTeacher || selectedSubject) ? "جرب اختيار فلتر مختلف أو امسح الفلاتر الحالية." : emptyDescription}
            </p>
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {filteredPackages.map((pkg) => (
            <Link
              prefetch={false}
              key={pkg.id}
              href={getHref(pkg.id)}
              className="group relative flex cursor-pointer flex-col overflow-hidden rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] text-right shadow-sm transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:-translate-y-1 hover:shadow-xl hover:shadow-[var(--admin-primary-10)] hover:border-[var(--admin-primary-30)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
            >
              {/* Image Frame */}
              <div className="relative aspect-video w-full overflow-hidden bg-[var(--admin-bg)]">
                <Image
                  src={pkg.imageUrl ? resolveMediaUrl(pkg.imageUrl) : '/images/default-package.webp'}
                  alt={pkg.name}
                  fill
                  className="object-cover transition-transform duration-700 ease-out group-hover:scale-[1.03]"
                  sizes="(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-[var(--admin-card)] to-transparent via-[var(--admin-card)]/20" />
                
                {/* Badge */}
                <div className="absolute left-4 top-4">
                  <span
                    className={`inline-flex items-center rounded-lg px-2.5 py-1 text-xs font-black uppercase tracking-wider shadow-md ${
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
                <div className="mb-3 flex items-end justify-between gap-3 border-b border-[var(--admin-border)] pb-3">
                  <h3 className="line-clamp-2 text-lg font-black leading-snug text-[var(--admin-text)] transition-colors group-hover:text-[var(--admin-primary)]">
                    {pkg.name}
                  </h3>
                </div>
                
                <p className="mb-4 line-clamp-2 text-xs font-medium leading-relaxed text-[var(--admin-muted)]">
                  {pkg.description || 'باقة تعليمية متكاملة لضمان التفوق الأكاديمي. تتضمن كافة الشروحات الضرورية.'}
                </p>

                {/* Teacher Info */}
                {pkg.teacherName && (
                  <div className="mb-4 flex items-center gap-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3">
                    {pkg.teacherProfileImageUrl ? (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img
                        src={resolveMediaUrl(pkg.teacherProfileImageUrl)}
                        alt={pkg.teacherName}
                        className="h-10 w-10 rounded-xl object-cover border border-[var(--admin-border)] shadow-sm"
                      />
                    ) : (
                      <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-black text-sm shadow-inner">
                        {pkg.teacherName.charAt(0)}
                      </div>
                    )}
                    <div className="min-w-0">
                      <p className="text-sm font-black text-[var(--admin-text)] truncate">أ. {pkg.teacherName}</p>
                      {pkg.subjectName && (
                        <p className="text-xs font-bold text-[var(--admin-primary)] truncate">{pkg.subjectName}</p>
                      )}
                    </div>
                  </div>
                )}

                <div className="mt-auto flex items-center justify-between">
                  <div className="flex flex-col">
                    <span className="text-xs font-bold text-[var(--admin-muted)]">السعر</span>
                    <span className="font-mono text-lg font-black text-[var(--admin-text)]">
                      {pkg.price.toFixed(0)} <span className="text-xs font-sans">ج.م</span>
                    </span>
                  </div>
                  
                  <div className={`flex h-10 items-center justify-center rounded-xl border px-5 text-xs font-bold transition-[color,background-color,border-color,opacity,transform,box-shadow] ${
                    pkg.isEnrolled 
                      ? "border-transparent bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary-20)] group-hover:bg-[var(--admin-primary-strong)]"
                      : "border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-text)] group-hover:border-[var(--admin-text)] group-hover:bg-[var(--admin-text)] group-hover:text-[var(--admin-bg)]"
                  }`}>
                    {actionLabel}
                  </div>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}

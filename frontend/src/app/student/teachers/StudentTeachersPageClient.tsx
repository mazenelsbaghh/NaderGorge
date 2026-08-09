"use client";

import { useCallback, useState } from "react";
import { motion } from "framer-motion";
import { GraduationCap, ChevronLeft, BookOpen, ArrowRight, BookOpenText, Layers, ShieldCheck } from "lucide-react";

import Image from "next/image";
import Link from "next/link";

import { studentService, type PublicTeacherDto } from "@/services/student-service";
import { contentService, type PackageDto } from "@/services/content-service";
import { resolveMediaUrl } from "@/utils/resolve-media-url";
import { GRADE_LEVEL_LABELS } from "@/lib/academic-labels";
import { usePlatformQuery } from "@/components/providers/QueryProvider";
import { queryKeys } from "@/lib/query-keys";
import { useAuthStore } from "@/stores/auth-store";

const GRADE_NAMES: { [key: string]: string } = {
  ...GRADE_LEVEL_LABELS,
  All: "جميع الصفوف الدراسية",
};

const stagger = {
  hidden: {},
  visible: { transition: { staggerChildren: 0.08, delayChildren: 0.1 } },
};

const fadeUp = {
  hidden: { opacity: 0, y: 15 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.4, ease: [0.16, 1, 0.3, 1] as const },
  },
};

export default function StudentTeachersPageClient() {
  // Flow states
  const [activeTeacher, setActiveTeacher] = useState<PublicTeacherDto | null>(null);
  const [activeGrade, setActiveGrade] = useState<string | null>(null);
  const userId = useAuthStore((state) => state.user?.id);
  const userBoundary = userId ?? 'pending';
  const teachersQueryFn = useCallback(
    ({ signal }: { signal: AbortSignal }) => studentService.getPublicTeachers(signal),
    []
  );
  const packagesQueryFn = useCallback(
    async ({ signal }: { signal: AbortSignal }) => {
      const response = await contentService.getPackages({ signal });
      return response.data?.data ?? [];
    },
    []
  );
  const teachersQuery = usePlatformQuery<PublicTeacherDto[]>({
    queryKey: queryKeys.student.teachers(userBoundary),
    queryFn: teachersQueryFn,
    staleTime: 60_000,
    enabled: Boolean(userId),
  });
  const packagesQuery = usePlatformQuery<PackageDto[]>({
    queryKey: queryKeys.student.packages(userBoundary),
    queryFn: packagesQueryFn,
    staleTime: 30_000,
    enabled: Boolean(userId),
  });
  const teachers = teachersQuery.data ?? [];
  const packages = packagesQuery.data ?? [];
  const loading =
    !userId ||
    (teachersQuery.data === undefined && teachersQuery.error === null) ||
    (packagesQuery.data === undefined && packagesQuery.error === null);
  const hasLoadError = Boolean(teachersQuery.error || packagesQuery.error);
  const refetch = () => {
    void Promise.all([
      teachersQuery.refetch(),
      packagesQuery.refetch(),
    ]).catch(() => undefined);
  };

  if (loading) {
    return (
      <div className="space-y-8 animate-pulse">
        {/* Banner Skeleton */}
        <div className="h-[200px] rounded-3xl bg-[var(--admin-card-strong)]" />

        {/* Grid Skeleton */}
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="h-80 rounded-2xl bg-[var(--admin-card-strong)]" />
          ))}
        </div>
      </div>
    );
  }

  if (hasLoadError && teachersQuery.data === undefined && packagesQuery.data === undefined) {
    return (
      <div role="alert" className="rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-6 text-center">
        <p className="font-bold text-[var(--admin-danger)]">تعذر تحميل بيانات المدرسين حاليًا.</p>
        <button type="button" onClick={refetch} className="mt-4 min-h-11 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)]">
          إعادة المحاولة
        </button>
      </div>
    );
  }

  // LEVEL 1: Render List of Teachers
  if (activeTeacher === null) {
    return (
      <motion.div
        className="space-y-10 pb-10"
        variants={stagger}
        initial="hidden"
        animate="visible"
        key="teachers-list"
      >
        {hasLoadError && (
          <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] px-4 py-3 text-sm font-bold text-[var(--admin-warning)]">
            <span>تعذر تحديث بعض البيانات؛ يتم عرض آخر نسخة متاحة.</span>
            <button type="button" onClick={refetch} className="min-h-11 rounded-xl border border-current px-4">
              إعادة المحاولة
            </button>
          </div>
        )}
        {/* Banner Section */}
        <motion.section
          variants={fadeUp}
          className="relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-7 shadow-sm sm:p-10"
        >
          <div className="absolute inset-y-0 right-0 hidden w-1 bg-[var(--admin-secondary,#0E8F8F)] lg:block" />
          <div className="absolute right-10 bottom-0 top-0 hidden w-40 opacity-[0.07] lg:block">
            <GraduationCap className="h-full w-full text-[var(--admin-primary)]" />
          </div>

          <div className="relative z-10 max-w-2xl">
            <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-bold text-[var(--admin-primary-strong)]">
              <GraduationCap className="h-3.5 w-3.5" />
              <span>نخبة من أفضل المعلمين</span>
            </div>
            <h1 className="text-3xl font-black text-[var(--admin-text)] sm:text-5xl">
              مدرسو المنصة
            </h1>
            <p className="mt-4 text-sm font-medium leading-relaxed text-[var(--admin-muted)] sm:text-base">
              تصفح معلمي المنصة والمسارات والمساقات التعليمية والباقات المتاحة معهم لتبدأ رحلتك التعليمية المتميزة.
            </p>
          </div>
        </motion.section>

        {/* Grid of Teachers */}
        {teachers.length === 0 ? (
          <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] py-16 text-center">
            <GraduationCap className="mb-4 h-16 w-16 text-[var(--admin-muted)] opacity-60" />
            <p className="font-bold text-[var(--admin-muted)]">لا يوجد معلمون متاحون حالياً.</p>
            <p className="mt-2 max-w-md text-sm font-medium leading-6 text-[var(--admin-muted)]">
              عند إضافة مدرسين نشطين على المنصة سيظهرون هنا تلقائياً.
            </p>
          </div>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
            {teachers.map((teacher) => {
              const teacherPackages = packages.filter(
                (p) => p.teacherId === teacher.id
              );
              const teacherProfileHref = `/student/teachers/${teacher.slug || teacher.teacherId || teacher.id}`;

              const specList = teacher.specialization
                ? teacher.specialization
                    .split(",")
                    .map((s) => GRADE_NAMES[s.trim()] || s.trim())
                : [];

              return (
                <motion.article
                  key={teacher.id}
                  variants={fadeUp}
                  className="group overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm transition hover:border-[var(--admin-primary)]"
                >
                  <Link href={teacherProfileHref} className="flex min-h-80 flex-col focus-visible:outline-none">
                    <div className="flex min-w-0 flex-1 flex-col p-4">
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-3">
                          <div className="relative flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-[var(--admin-card-soft)] text-lg font-black text-[var(--admin-primary)]">
                            {teacher.profileImageUrl ? (
                              <Image
                                src={resolveMediaUrl(teacher.profileImageUrl)}
                                alt={teacher.fullName}
                                fill
                                className="object-cover transition duration-200 group-hover:scale-[1.03]"
                                sizes="48px"
                              />
                            ) : (
                              teacher.fullName.charAt(0)
                            )}
                          </div>
                          <div className="min-w-0 flex-1">
                            <h2 className="truncate text-base font-black text-[var(--admin-text)] transition-colors group-hover:text-[var(--admin-primary)]">
                              أ. {teacher.fullName}
                            </h2>
                            <div className="mt-1 flex flex-wrap gap-1">
                              {(teacher.subjectNames || []).slice(0, 2).map((subject, idx) => (
                                <span key={idx} className="rounded-full bg-[var(--admin-primary-15)] px-2 py-0.5 text-sm font-bold text-[var(--admin-primary)]">
                                  {subject}
                                </span>
                              ))}
                            </div>
                          </div>
                          <ShieldCheck className="h-4 w-4 shrink-0 text-[var(--admin-primary)]" />
                        </div>

                        {teacher.bio ? (
                          <p className="mt-3 line-clamp-2 text-sm font-medium leading-6 text-[var(--admin-muted)]">
                            {teacher.bio}
                          </p>
                        ) : (
                          <p className="mt-3 line-clamp-2 text-sm font-medium leading-6 text-[var(--admin-muted)]">
                            لا يوجد وصف متوفر حالياً للمعلم.
                          </p>
                        )}

                        <div className="mt-4 space-y-2">
                          <div className="flex items-center gap-2 text-xs font-black text-[var(--admin-text)]">
                            <GraduationCap className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                            <span>المراحل المتاحة</span>
                          </div>
                          <div className="flex flex-wrap gap-1">
                            {(specList.length > 0 ? specList : ["جميع الصفوف"]).slice(0, 3).map((spec, sIdx) => (
                              <span
                                key={sIdx}
                                className="rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-2 py-0.5 text-sm font-bold text-[var(--admin-muted)]"
                              >
                                {spec}
                              </span>
                            ))}
                          </div>
                        </div>
                      </div>

                      <div className="mt-4 flex items-center justify-between gap-2 border-t border-[var(--admin-border)] pt-3">
                        <div className="flex items-center gap-2 text-xs font-bold text-[var(--admin-muted)]">
                          <Layers className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                          <span>{teacherPackages.length} باقة متاحة</span>
                        </div>
                        <span className="inline-flex min-h-9 items-center gap-1.5 rounded-lg bg-[var(--admin-primary)] px-3 text-xs font-black text-[var(--admin-primary-contrast)] transition group-hover:bg-[var(--admin-primary-strong)]">
                          الملف
                          <ChevronLeft className="h-4 w-4 transition-transform group-hover:-translate-x-1" />
                        </span>
                      </div>
                    </div>
                  </Link>
                </motion.article>
              );
            })}
          </div>
        )}
      </motion.div>
    );
  }

  // Filter packages belonging to the active teacher
  const teacherPackages = packages.filter(
    (p) => p.teacherId === activeTeacher.id
  );

  const getPackageGrades = (pkg: PackageDto) => (pkg.targetGrade || "All")
    .split(',')
    .map((grade) => grade.trim())
    .filter(Boolean);

  const uniqueGrades = Array.from(new Set(teacherPackages.flatMap(getPackageGrades)));

  // LEVEL 2: Render Grade Levels of Selected Teacher
  if (activeGrade === null) {
    return (
      <motion.div
        className="space-y-8 pb-10"
        variants={stagger}
        initial="hidden"
        animate="visible"
        key="teacher-grades"
      >
        {/* Back and Header */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between border-b border-[var(--admin-border)]/50 pb-6">
          <div className="flex items-center gap-3">
            <button
              onClick={() => setActiveTeacher(null)}
              className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)] transition-[color,background-color,border-color,opacity,transform,box-shadow]"
              title="عودة للمعلمين"
            >
              <ArrowRight className="h-5 w-5" />
            </button>
            <div>
              <h1 className="text-2xl font-black text-[var(--admin-text)]">
                أ. {activeTeacher.fullName}
              </h1>
              <p className="text-xs text-[var(--admin-muted)] mt-1">
                اختر الصف الدراسي لتصفح الباقات والمحتوى التعليمي
              </p>
            </div>
          </div>
          <Link
            href={`/student/teachers/${activeTeacher.id}`}
            className="inline-flex h-10 items-center justify-center rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white"
          >
            فتح بروفايل المدرس
          </Link>
        </div>

        {/* Teacher profile summary card */}
        <div className="overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <div className="flex flex-col sm:flex-row gap-6 items-start">
            {activeTeacher.profileImageUrl ? (
              <div className="relative h-20 w-20 overflow-hidden rounded-3xl border border-[var(--admin-border)] shadow-sm flex-shrink-0">
                <Image
                  src={resolveMediaUrl(activeTeacher.profileImageUrl)}
                  alt={activeTeacher.fullName}
                  fill
                  className="object-cover"
                  sizes="80px"
                />
              </div>
            ) : (
              <div className="flex h-20 w-20 items-center justify-center rounded-3xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-extrabold text-3xl flex-shrink-0">
                {activeTeacher.fullName.charAt(0)}
              </div>
            )}
            <div className="space-y-2">
              <div className="flex flex-wrap gap-1">
                {activeTeacher.subjectNames?.map((subject, idx) => (
                  <span
                    key={idx}
                    className="inline-flex items-center rounded-md bg-[var(--admin-primary-10)] px-2.5 py-0.5 text-xs font-bold text-[var(--admin-primary)]"
                  >
                    {subject}
                  </span>
                ))}
              </div>
              <p className="text-sm font-medium leading-relaxed text-[var(--admin-text)]">
                {activeTeacher.bio || "لا يوجد وصف متوفر حالياً للمعلم."}
              </p>
            </div>
          </div>
        </div>

        {/* Grades Grid */}
        <div className="space-y-6">
          <h2 className="text-lg font-black text-[var(--admin-text)] flex items-center gap-2">
            <GraduationCap className="h-5 w-5 text-[var(--admin-primary)]" />
            <span>الصفوف الدراسية المتاحة</span>
          </h2>

          {uniqueGrades.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] py-16 text-center">
              <BookOpen className="mb-4 h-12 w-12 text-[var(--admin-muted)] opacity-60" />
              <p className="font-bold text-[var(--admin-muted)]">لا توجد باقات دراسية معلنة لهذا المعلم حالياً.</p>
            </div>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {uniqueGrades.map((grade) => {
                const gradePackagesCount = teacherPackages.filter((pkg) => getPackageGrades(pkg).includes(grade)).length;

                return (
                  <motion.button
                    key={grade}
                    variants={fadeUp}
                    onClick={() => setActiveGrade(grade)}
                    className="group flex items-center justify-between rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 text-right transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:border-[var(--admin-primary-30)] hover:shadow-md hover:scale-[1.02]"
                  >
                    <div className="flex items-center gap-4">
                      <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[var(--admin-primary-10)] text-[var(--admin-primary)] group-hover:bg-[var(--admin-primary)] group-hover:text-white transition-colors">
                        <GraduationCap className="h-6 w-6" />
                      </div>
                      <div>
                        <span className="block text-sm font-extrabold text-[var(--admin-text)] group-hover:text-[var(--admin-primary)] transition-colors">
                          {GRADE_NAMES[grade] || grade}
                        </span>
                        <span className="block text-xs text-[var(--admin-muted)] mt-1">
                          {gradePackagesCount} باقة تعليمية
                        </span>
                      </div>
                    </div>
                    <ChevronLeft className="h-5 w-5 text-[var(--admin-muted)] transition-transform group-hover:-translate-x-1" />
                  </motion.button>
                );
              })}
            </div>
          )}
        </div>
      </motion.div>
    );
  }

  // LEVEL 3: Render Packages for Selected Teacher & Grade
  const filteredPackages = teacherPackages.filter((pkg) => getPackageGrades(pkg).includes(activeGrade));

  return (
    <motion.div
      className="space-y-8 pb-10"
      variants={stagger}
      initial="hidden"
      animate="visible"
      key="teacher-grade-packages"
    >
      {/* Back and Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between border-b border-[var(--admin-border)]/50 pb-6">
        <div className="flex items-center gap-3">
          <button
            onClick={() => setActiveGrade(null)}
            className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)] transition-[color,background-color,border-color,opacity,transform,box-shadow]"
            title="عودة للصفوف الدراسية"
          >
            <ArrowRight className="h-5 w-5" />
          </button>
          <div>
            <h1 className="text-2xl font-black text-[var(--admin-text)]">
              باقات {GRADE_NAMES[activeGrade] || activeGrade}
            </h1>
            <p className="text-xs text-[var(--admin-muted)] mt-1">
              المعلم: أ. {activeTeacher.fullName}
            </p>
          </div>
        </div>
      </div>

      {/* Packages Grid */}
      <div className="space-y-6">
        <h2 className="text-lg font-black text-[var(--admin-text)] flex items-center gap-2">
          <BookOpenText className="h-5 w-5 text-[var(--admin-primary)]" />
          <span>الباقات المتاحة للمشاهدة والاشتراك</span>
        </h2>

        {filteredPackages.length === 0 ? (
          <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] py-16 text-center">
            <BookOpen className="mb-4 h-12 w-12 text-[var(--admin-muted)] opacity-60" />
            <p className="font-bold text-[var(--admin-muted)]">لا توجد باقات متوفرة لهذا الصف الدراسي حالياً.</p>
          </div>
        ) : (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {filteredPackages.map((pkg) => (
              <Link
                key={pkg.id}
                href={`/student/packages/${pkg.id}`}
                prefetch={false}
                className="group flex flex-col justify-between overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:border-[var(--admin-primary-30)] hover:shadow-lg hover:shadow-[var(--admin-primary-10)] hover:scale-[1.01] cursor-pointer"
              >
                <div>
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex items-center gap-3">
                      <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                        <BookOpen className="h-6 w-6" />
                      </div>
                      <div>
                        <h3 className="text-sm font-black text-[var(--admin-text)] group-hover:text-[var(--admin-primary)] transition-colors line-clamp-1">
                          {pkg.name}
                        </h3>
                        <span className="text-xs text-[var(--admin-muted)] block mt-0.5">
                          {pkg.subjectName}
                        </span>
                      </div>
                    </div>

                    <span
                      className={`rounded-md px-2 py-0.5 text-xs font-black tracking-wide ${
                        pkg.isEnrolled
                          ? "bg-[var(--admin-success-20)] text-[var(--admin-success)]"
                          : "bg-[var(--admin-card-strong)] text-[var(--admin-text)]"
                      }`}
                    >
                      {pkg.isEnrolled ? "مفعّلة" : "غير مفعّلة"}
                    </span>
                  </div>

                  <p className="text-xs text-[var(--admin-muted)] leading-relaxed mt-4 line-clamp-3">
                    {pkg.description || "لا يوجد وصف متوفر للباقة."}
                  </p>
                </div>

                <div className="mt-6 border-t border-[var(--admin-border)]/50 pt-4 flex items-center justify-between">
                  <div className="flex flex-col">
                    <span className="text-xs text-[var(--admin-muted)]">سعر الاشتراك</span>
                    <span className="text-sm font-black text-[var(--admin-text)]">
                      {pkg.price.toFixed(0)} ج.م
                    </span>
                  </div>

                  <span className="inline-flex items-center gap-1 text-xs font-bold text-[var(--admin-primary)] group-hover:text-[var(--admin-primary-strong)] transition-colors">
                    <span>عرض التفاصيل</span>
                    <ChevronLeft className="h-4 w-4 transition-transform group-hover:-translate-x-1" />
                  </span>
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>
    </motion.div>
  );
}

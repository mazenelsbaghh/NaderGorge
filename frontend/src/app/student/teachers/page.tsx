"use client";

import { useEffect, useState } from "react";
import { motion } from "framer-motion";
import { GraduationCap, ChevronLeft, BookOpen } from "lucide-react";
import { useRouter } from "next/navigation";
import Image from "next/image";

import { studentService, type PublicTeacherDto } from "@/services/student-service";
import { contentService, type PackageDto } from "@/services/content-service";
import { resolveMediaUrl } from "@/utils/resolve-media-url";
import { devConsole } from "@/utils/dev-console";

const GRADE_NAMES: Record<string, string> = {
  FirstSecondary: "الأول الثانوي",
  SecondSecondary: "الثاني الثانوي",
  SecondaryGrade3: "الثالث الثانوي",
  FirstBaccalaureate: "الأول بكالوريا",
  SecondBaccalaureate: "الثاني بكالوريا",
  PrimaryGrade1: "الأول الابتدائي",
  PrimaryGrade2: "الثاني الابتدائي",
  PrimaryGrade3: "الثالث الابتدائي",
  PrimaryGrade4: "الرابع الابتدائي",
  PrimaryGrade5: "الخامس الابتدائي",
  PrimaryGrade6: "السادس الابتدائي",
  PrepGrade1: "الأول الإعدادي",
  PrepGrade2: "الثاني الإعدادي",
  PrepGrade3: "الثالث الإعدادي",
  AzhariPrimary1: "الأول الابتدائي الأزهري",
  AzhariPrep1: "الأول الإعدادي الأزهري",
  AzhariSecondary1: "الأول الثانوي الأزهري",
  AmericanGrade9: "Grade 9",
  AmericanGrade10: "Grade 10",
  AmericanGrade11: "Grade 11",
  AmericanGrade12: "Grade 12",
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

export default function StudentTeachersPage() {
  const router = useRouter();
  const [teachers, setTeachers] = useState<PublicTeacherDto[]>([]);
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      studentService.getPublicTeachers(),
      contentService.getPackages(),
    ])
      .then(([teachersData, packagesRes]) => {
        setTeachers(teachersData || []);
        setPackages(packagesRes.data?.data || []);
      })
      .catch((err) => {
        devConsole.error("Error loading teachers/packages:", err);
      })
      .finally(() => {
        setLoading(false);
      });
  }, []);

  if (loading) {
    return (
      <div className="space-y-8 animate-pulse">
        {/* Banner Skeleton */}
        <div className="h-[200px] rounded-3xl bg-[var(--admin-card-strong)]" />
        
        {/* Grid Skeleton */}
        <div className="grid gap-6 md:grid-cols-2">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="h-96 rounded-[2rem] bg-[var(--admin-card-strong)]" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <motion.div
      className="space-y-10 pb-10"
      variants={stagger}
      initial="hidden"
      animate="visible"
    >
      {/* ── Banner Section ── */}
      <motion.section
        variants={fadeUp}
        className="relative overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm sm:p-10"
      >
        <div className="absolute -left-32 -top-32 h-96 w-96 rounded-full bg-[var(--admin-primary-15)] blur-[48px]" />
        <div className="absolute right-10 bottom-0 top-0 hidden w-40 opacity-10 lg:block">
          <GraduationCap className="h-full w-full text-[var(--admin-primary)]" />
        </div>
        
        <div className="relative z-10 max-w-2xl">
          <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary-20)] bg-[var(--admin-primary-10)] px-3 py-1 text-xs font-bold text-[var(--admin-primary-strong)]">
            <GraduationCap className="h-3.5 w-3.5" />
            <span>نخبة من أفضل المعلمين</span>
          </div>
          <h1 className="text-3xl font-black tracking-tight text-[var(--admin-text)] sm:text-5xl">
            مدرسو المنصة
          </h1>
          <p className="mt-4 text-sm font-medium leading-relaxed text-[var(--admin-muted)] sm:text-base">
            تصفح معلمي المنصة والمسارات والمساقات التعليمية والباقات المتاحة معهم لتبدأ رحلتك التعليمية المتميزة.
          </p>
        </div>
      </motion.section>

      {/* ── Grid of Teachers ── */}
      {teachers.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-[2rem] border border-dashed border-[var(--admin-border)] py-16 text-center">
          <GraduationCap className="mb-4 h-16 w-16 text-[var(--admin-muted)] opacity-60" />
          <p className="font-bold text-[var(--admin-muted)]">لا يوجد معلمون مسجلون في المنصة حالياً.</p>
        </div>
      ) : (
        <div className="grid gap-6 md:grid-cols-2">
          {teachers.map((teacher) => {
            const teacherPackages = packages.filter(
              (p) => p.teacherId === teacher.id
            );

            const specList = teacher.specialization
              ? teacher.specialization
                  .split(",")
                  .map((s) => GRADE_NAMES[s.trim()] || s.trim())
              : [];

            return (
              <motion.div
                key={teacher.id}
                variants={fadeUp}
                className="group flex flex-col justify-between overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm transition-all hover:border-[var(--admin-primary-30)] hover:shadow-lg hover:shadow-[var(--admin-primary-10)]"
              >
                <div>
                  {/* Teacher Profile Info */}
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex items-center gap-4">
                      {teacher.profileImageUrl ? (
                        <div className="relative h-16 w-16 overflow-hidden rounded-2xl border border-[var(--admin-border)] shadow-sm">
                          <Image
                            src={resolveMediaUrl(teacher.profileImageUrl)}
                            alt={teacher.fullName}
                            fill
                            className="object-cover"
                            sizes="64px"
                          />
                        </div>
                      ) : (
                        <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-extrabold text-xl">
                          {teacher.fullName.charAt(0)}
                        </div>
                      )}

                      <div>
                        <h2 className="text-lg font-black text-[var(--admin-text)] group-hover:text-[var(--admin-primary)] transition-colors">
                          أ. {teacher.fullName}
                        </h2>
                        
                        {/* Subjects tag list */}
                        {teacher.subjectNames && teacher.subjectNames.length > 0 && (
                          <div className="mt-1 flex flex-wrap gap-1">
                            {teacher.subjectNames.map((subject, idx) => (
                              <span
                                key={idx}
                                className="inline-flex items-center rounded-md bg-[var(--admin-primary-10)] px-2 py-0.5 text-[10px] font-bold text-[var(--admin-primary)]"
                              >
                                {subject}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>
                    </div>
                  </div>

                  {/* Specializations & Bio */}
                  <div className="mt-4 space-y-3">
                    {specList.length > 0 && (
                      <div className="flex flex-wrap gap-1.5 items-center">
                        <span className="text-xs font-bold text-[var(--admin-muted)]">المراحل:</span>
                        <div className="flex flex-wrap gap-1">
                          {specList.map((spec, sIdx) => (
                            <span
                              key={sIdx}
                              className="rounded-full bg-[var(--admin-card-soft)] px-2.5 py-0.5 text-[10px] font-semibold text-[var(--admin-muted)] border border-[var(--admin-border)]/40"
                            >
                              {spec}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}

                    {teacher.bio ? (
                      <p className="text-xs leading-relaxed text-[var(--admin-muted)] line-clamp-3">
                        {teacher.bio}
                      </p>
                    ) : (
                      <p className="text-xs text-[var(--admin-muted)]/60 italic">
                        لا يوجد وصف متوفر حالياً للمعلم.
                      </p>
                    )}
                  </div>
                </div>

                {/* Packages Section */}
                <div className="mt-6 border-t border-[var(--admin-border)]/50 pt-5">
                  <div className="mb-3 flex items-center justify-between">
                    <h3 className="flex items-center gap-1.5 text-xs font-black text-[var(--admin-text)]">
                      <BookOpen className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                      <span>الباقات المتاحة ({teacherPackages.length})</span>
                    </h3>
                  </div>

                  {teacherPackages.length === 0 ? (
                    <p className="text-[11px] text-[var(--admin-muted)]/60 italic">
                      لا توجد باقات معلنة لهذا المعلم بعد.
                    </p>
                  ) : (
                    <div className="space-y-2">
                      {teacherPackages.slice(0, 3).map((pkg) => (
                        <button
                          key={pkg.id}
                          type="button"
                          onClick={() => router.push(`/student/packages/${pkg.id}`)}
                          className="flex w-full items-center justify-between rounded-xl border border-[var(--admin-border)]/60 bg-[var(--admin-card-soft)] p-3 text-right transition-all hover:bg-[var(--admin-hover)] hover:border-[var(--admin-primary-20)]"
                        >
                          <div className="flex flex-col gap-0.5 max-w-[70%]">
                            <span className="line-clamp-1 text-xs font-extrabold text-[var(--admin-text)]">
                              {pkg.name}
                            </span>
                            <span className="text-[10px] text-[var(--admin-muted)]">
                              {pkg.price.toFixed(0)} ج.م
                            </span>
                          </div>

                          <div className="flex items-center gap-2">
                            <span
                              className={`rounded-md px-2 py-0.5 text-[9px] font-black tracking-wide ${
                                pkg.isEnrolled
                                  ? "bg-[var(--admin-success-20)] text-[var(--admin-success)]"
                                  : "bg-[var(--admin-card-strong)] text-[var(--admin-text)]"
                              }`}
                            >
                              {pkg.isEnrolled ? "مفعّلة" : "غير مفعّلة"}
                            </span>
                            <ChevronLeft className="h-3.5 w-3.5 text-[var(--admin-muted)]" />
                          </div>
                        </button>
                      ))}

                      {teacherPackages.length > 3 && (
                        <button
                          type="button"
                          onClick={() => router.push(`/student/packages`)}
                          className="w-full text-center text-[10px] font-bold text-[var(--admin-primary)] hover:underline mt-1"
                        >
                          عرض كل الباقات الأخرى للمعلم...
                        </button>
                      )}
                    </div>
                  )}
                </div>
              </motion.div>
            );
          })}
        </div>
      )}
    </motion.div>
  );
}

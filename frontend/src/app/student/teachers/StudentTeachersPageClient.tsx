"use client";

import { useEffect, useState } from "react";
import { motion } from "framer-motion";
import { GraduationCap, ChevronLeft, BookOpen, ArrowRight, BookOpenText } from "lucide-react";

import Image from "next/image";
import Link from "next/link";

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

  const [teachers, setTeachers] = useState<PublicTeacherDto[]>([]);
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [loading, setLoading] = useState(true);

  // Flow states
  const [activeTeacher, setActiveTeacher] = useState<PublicTeacherDto | null>(null);
  const [activeGrade, setActiveGrade] = useState<string | null>(null);

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
        {/* Banner Section */}
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

        {/* Grid of Teachers */}
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
                  onClick={() => {
                    setActiveTeacher(teacher);
                    setActiveGrade(null);
                  }}
                  className="group flex flex-col justify-between overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm transition-all hover:border-[var(--admin-primary-30)] hover:shadow-lg hover:shadow-[var(--admin-primary-10)] hover:scale-[1.01] cursor-pointer"
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
                                  className="inline-flex items-center rounded-md bg-[var(--admin-primary-10)] px-2 py-0.5 text-xs font-bold text-[var(--admin-primary)]"
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
                                className="rounded-full bg-[var(--admin-card-soft)] px-2.5 py-0.5 text-xs font-semibold text-[var(--admin-muted)] border border-[var(--admin-border)]/40"
                              >
                                {spec}
                              </span>
                            ))}
                          </div>
                        </div>
                      )}

                      {teacher.bio ? (
                        <p className="text-xs leading-relaxed text-[var(--admin-muted)] line-clamp-2">
                          {teacher.bio}
                        </p>
                      ) : (
                        <p className="text-xs text-[var(--admin-muted)]/60 italic">
                          لا يوجد وصف متوفر حالياً للمعلم.
                        </p>
                      )}
                    </div>
                  </div>

                  {/* Flow Action Prompt */}
                  <div className="mt-6 border-t border-[var(--admin-border)]/50 pt-4 flex items-center justify-between text-xs font-bold text-[var(--admin-primary)] group-hover:text-[var(--admin-primary-strong)] transition-colors">
                    <span>تصفح الصفوف الدراسية والباقات ({teacherPackages.length})</span>
                    <ChevronLeft className="h-4 w-4 transition-transform group-hover:-translate-x-1" />
                  </div>
                </motion.div>
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

  const uniqueGrades = Array.from(
    new Set(teacherPackages.map((p) => p.targetGrade || "All"))
  );

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
              className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)] transition-all"
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
        </div>

        {/* Teacher profile summary card */}
        <div className="overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
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
            <div className="flex flex-col items-center justify-center rounded-[2rem] border border-dashed border-[var(--admin-border)] py-16 text-center">
              <BookOpen className="mb-4 h-12 w-12 text-[var(--admin-muted)] opacity-60" />
              <p className="font-bold text-[var(--admin-muted)]">لا توجد باقات دراسية معلنة لهذا المعلم حالياً.</p>
            </div>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {uniqueGrades.map((grade) => {
                const gradePackagesCount = teacherPackages.filter(
                  (p) => (p.targetGrade || "All") === grade
                ).length;

                return (
                  <motion.button
                    key={grade}
                    variants={fadeUp}
                    onClick={() => setActiveGrade(grade)}
                    className="group flex items-center justify-between rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 text-right transition-all hover:border-[var(--admin-primary-30)] hover:shadow-md hover:scale-[1.02]"
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
  const filteredPackages = teacherPackages.filter(
    (p) => (p.targetGrade || "All") === activeGrade
  );

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
            className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)] transition-all"
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
          <div className="flex flex-col items-center justify-center rounded-[2rem] border border-dashed border-[var(--admin-border)] py-16 text-center">
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
                className="group flex flex-col justify-between overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm transition-all hover:border-[var(--admin-primary-30)] hover:shadow-lg hover:shadow-[var(--admin-primary-10)] hover:scale-[1.01] cursor-pointer"
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

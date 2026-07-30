'use client';

import Image from 'next/image';
import Link from 'next/link';
import {
  ArrowLeft,
  GraduationCap,
  ListFilter,
  Search,
  Star,
  X,
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import {
  studentService,
  type PublicTeacherDto,
} from '@/services/student-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import {
  GRADES_BY_STAGE,
  STAGE_OPTIONS,
  type EducationStage,
} from '@/lib/academic-labels';

type TeacherFilters = {
  stage: EducationStage | '';
  grade: string;
  subject: string;
};

const EMPTY_FILTERS: TeacherFilters = { stage: '', grade: '', subject: '' };

function normalize(value: string) {
  return value.trim().toLocaleLowerCase('ar');
}

function teacherSearchText(teacher: PublicTeacherDto) {
  return normalize(
    [
      teacher.fullName,
      teacher.displayName,
      teacher.specialization,
      ...(teacher.subjectNames || []),
    ]
      .filter(Boolean)
      .join(' ')
  );
}

function teacherMatchesStage(teacherText: string, stage: EducationStage) {
  return GRADES_BY_STAGE[stage]
    .flatMap((group) => group.grades)
    .some(
      (grade) =>
        teacherText.includes(normalize(grade.value)) ||
        teacherText.includes(normalize(grade.label))
    );
}

export default function PublicTeachersPageClient() {
  const [teachers, setTeachers] = useState<PublicTeacherDto[]>([]);
  const [query, setQuery] = useState('');
  const [filters, setFilters] = useState<TeacherFilters>(EMPTY_FILTERS);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    studentService
      .getPublicTeachers()
      .then(setTeachers)
      .finally(() => setLoading(false));
  }, []);

  const availableSubjects = useMemo(
    () =>
      [
        ...new Set(
          teachers
            .flatMap((teacher) => teacher.subjectNames || [])
            .map((subject) => subject.trim())
            .filter(Boolean)
        ),
      ].sort((first, second) => first.localeCompare(second, 'ar')),
    [teachers]
  );
  const availableGrades = filters.stage
    ? GRADES_BY_STAGE[filters.stage].flatMap((group) => group.grades)
    : [];
  const visibleTeachers = useMemo(() => {
    const queryValue = normalize(query);
    return teachers.filter((teacher) => {
      const searchText = teacherSearchText(teacher);
      const matchesQuery = !queryValue || searchText.includes(queryValue);
      const matchesStage =
        !filters.stage || teacherMatchesStage(searchText, filters.stage);
      const matchesGrade =
        !filters.grade || searchText.includes(normalize(filters.grade));
      const matchesSubject =
        !filters.subject ||
        (teacher.subjectNames || []).some(
          (subject) => normalize(subject) === normalize(filters.subject)
        );
      return matchesQuery && matchesStage && matchesGrade && matchesSubject;
    });
  }, [filters, query, teachers]);
  const hasActiveFilters = Boolean(
    query || filters.stage || filters.grade || filters.subject
  );

  return (
    <main
      className="min-h-screen bg-[var(--public-canvas)] px-4 pb-16 pt-28 text-[var(--public-text)] sm:px-6 sm:pt-32 lg:px-10"
      dir="rtl"
    >
      <div className="mx-auto max-w-6xl">
        <header className="border-b border-[var(--public-border)] pb-7">
          <div>
            <Link
              href="/"
              className="inline-flex min-h-11 items-center gap-2 text-sm font-black text-[var(--public-accent)] hover:text-[var(--public-primary)]"
            >
              <ArrowLeft className="h-4 w-4" /> الرئيسية
            </Link>
            <h1 className="mt-4 text-3xl font-black sm:text-4xl">
              اختَر معلمك وابدأ رحلتك
            </h1>
            <p className="mt-3 max-w-2xl text-sm font-bold leading-7 text-[var(--public-text-muted)]">
              فلتر المعلمين حسب المرحلة والصف والمادة، ثم شاهد بروفايل المعلم
              وباقاته.
            </p>
          </div>
        </header>

        <section
          className="mt-6 grid gap-3 rounded-2xl border border-[var(--public-border)] bg-[var(--public-surface)] p-4 shadow-[0_8px_14px_rgba(10,29,61,0.05)] lg:grid-cols-[minmax(15rem,1.35fr)_repeat(3,minmax(0,1fr))_auto] lg:items-end"
          aria-label="فلاتر المعلمين"
        >
          <label className="relative block">
            <span className="mb-2 block text-xs font-black text-[var(--public-text-muted)]">
              البحث
            </span>
            <Search className="pointer-events-none absolute right-4 top-[calc(50%+0.65rem)] h-4 w-4 -translate-y-1/2 text-[var(--public-accent)]" />
            <input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              className="h-12 w-full rounded-xl border border-[var(--public-border)] bg-[var(--public-surface-muted)] pr-11 pl-4 text-sm font-bold outline-none transition focus:border-[var(--public-accent)] focus:ring-2 focus:ring-[var(--public-accent)]/20"
              placeholder="اسم المعلم أو المادة"
            />
          </label>
          <label className="block">
            <span className="mb-2 block text-xs font-black text-[var(--public-text-muted)]">
              المرحلة
            </span>
            <select
              value={filters.stage}
              onChange={(event) =>
                setFilters({
                  stage: event.target.value as EducationStage | '',
                  grade: '',
                  subject: filters.subject,
                })
              }
              className="h-12 w-full rounded-xl border border-[var(--public-border)] bg-[var(--public-surface-muted)] px-3 text-sm font-bold outline-none transition focus:border-[var(--public-accent)] focus:ring-2 focus:ring-[var(--public-accent)]/20"
            >
              <option value="">كل المراحل</option>
              {STAGE_OPTIONS.map((stage) => (
                <option key={stage.value} value={stage.value}>
                  {stage.label}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="mb-2 block text-xs font-black text-[var(--public-text-muted)]">
              الصف
            </span>
            <select
              value={filters.grade}
              disabled={!filters.stage}
              onChange={(event) =>
                setFilters({ ...filters, grade: event.target.value })
              }
              className="h-12 w-full rounded-xl border border-[var(--public-border)] bg-[var(--public-surface-muted)] px-3 text-sm font-bold outline-none transition disabled:cursor-not-allowed disabled:opacity-55 focus:border-[var(--public-accent)] focus:ring-2 focus:ring-[var(--public-accent)]/20"
            >
              <option value="">كل الصفوف</option>
              {availableGrades.map((grade) => (
                <option key={grade.value} value={grade.value}>
                  {grade.label}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="mb-2 block text-xs font-black text-[var(--public-text-muted)]">
              المادة
            </span>
            <select
              value={filters.subject}
              onChange={(event) =>
                setFilters({ ...filters, subject: event.target.value })
              }
              className="h-12 w-full rounded-xl border border-[var(--public-border)] bg-[var(--public-surface-muted)] px-3 text-sm font-bold outline-none transition focus:border-[var(--public-accent)] focus:ring-2 focus:ring-[var(--public-accent)]/20"
            >
              <option value="">كل المواد</option>
              {availableSubjects.map((subject) => (
                <option key={subject} value={subject}>
                  {subject}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            onClick={() => {
              setQuery('');
              setFilters(EMPTY_FILTERS);
            }}
            disabled={!hasActiveFilters}
            className="inline-flex h-12 items-center justify-center gap-2 rounded-xl border border-[var(--public-border)] px-4 text-sm font-black text-[var(--public-text)] transition hover:border-[var(--public-accent)] hover:text-[var(--public-accent)] disabled:cursor-not-allowed disabled:opacity-45"
          >
            <X className="h-4 w-4" /> مسح
          </button>
        </section>

        <div className="mt-7 flex items-center justify-between gap-3">
          <p className="text-sm font-black text-[var(--public-text-muted)]">
            {loading
              ? 'جارٍ تحميل المعلمين...'
              : `${visibleTeachers.length} ${visibleTeachers.length === 1 ? 'معلم متاح' : 'معلمين متاحين'}`}
          </p>
          <span className="inline-flex items-center gap-2 text-xs font-black text-[var(--public-accent)]">
            <ListFilter className="h-4 w-4" /> نتائج تناسب اختيارك
          </span>
        </div>

        <section
          className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-3"
          aria-live="polite"
        >
          {loading
            ? Array.from({ length: 6 }).map((_, index) => (
                <div
                  key={index}
                  className="h-80 animate-pulse rounded-2xl bg-[var(--public-surface-muted)]"
                />
              ))
            : visibleTeachers.map((teacher) => {
                const href = `/teachers/${teacher.slug || teacher.teacherId || teacher.id}`;
                const image = teacher.profileImageUrl
                  ? resolveMediaUrl(teacher.profileImageUrl)
                  : '';
                return (
                  <Link
                    key={teacher.id}
                    href={href}
                    className="group overflow-hidden rounded-2xl bg-[var(--public-surface)] transition hover:-translate-y-1 hover:shadow-[0_8px_18px_rgba(10,29,61,0.12)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-[var(--public-accent)]"
                  >
                    <div className="relative aspect-[4/3] bg-[var(--public-surface-muted)] p-3">
                      {image ? (
                        <Image
                          src={image}
                          alt={teacher.fullName}
                          fill
                          sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 33vw"
                          className="object-contain p-3 transition duration-300 group-hover:scale-[1.02]"
                        />
                      ) : (
                        <div className="grid h-full place-items-center rounded-xl bg-[var(--public-primary)] text-5xl font-black text-[var(--public-surface)]">
                          {teacher.fullName.charAt(0)}
                        </div>
                      )}
                      <span className="absolute bottom-3 right-3 inline-flex items-center gap-1 rounded-lg bg-[var(--public-primary)] px-2.5 py-1.5 text-xs font-black text-[var(--public-surface)]">
                        <Star className="h-3.5 w-3.5 fill-[#D4A017] text-[#D4A017]" />{' '}
                        {(teacher.ratingAverage || 0).toFixed(1)}
                      </span>
                    </div>
                    <div className="p-5">
                      <h2 className="text-lg font-black">
                        أ. {teacher.fullName}
                      </h2>
                      <p className="mt-1 truncate text-sm font-bold text-[var(--public-text-muted)]">
                        {teacher.specialization ||
                          teacher.subjectNames?.join('، ') ||
                          'معلم على منصة مسار'}
                      </p>
                      <span className="mt-5 inline-flex min-h-11 items-center gap-2 text-sm font-black text-[var(--public-accent)]">
                        عرض البروفايل{' '}
                        <ArrowLeft className="h-4 w-4 transition group-hover:-translate-x-1" />
                      </span>
                    </div>
                  </Link>
                );
              })}
        </section>
        {!loading && visibleTeachers.length === 0 ? (
          <div className="mt-8 rounded-2xl border border-dashed border-[var(--public-border)] bg-[var(--public-surface)] p-10 text-center">
            <GraduationCap className="mx-auto h-9 w-9 text-[var(--public-accent)]" />
            <p className="mt-3 font-black">لا يوجد معلم مطابق للبحث.</p>
          </div>
        ) : null}
      </div>
    </main>
  );
}

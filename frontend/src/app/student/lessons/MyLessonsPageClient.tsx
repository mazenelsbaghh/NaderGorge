'use client';

import { useCallback, useMemo, useState } from 'react';
import Link from 'next/link';
import {
  BookOpen,
  CheckCircle2,
  ChevronLeft,
  CirclePlay,
  Search,
} from 'lucide-react';
import { usePlatformQuery } from '@/components/providers/QueryProvider';
import { queryKeys } from '@/lib/query-keys';
import { studentService, type MyLessonDto } from '@/services/student-service';
import { useAuthStore } from '@/stores/auth-store';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

type LessonFilter = 'all' | 'in-progress' | 'completed';
const FILTERS: Array<{ value: LessonFilter; label: string }> = [
  { value: 'all', label: 'الكل' },
  { value: 'in-progress', label: 'لم تكتمل' },
  { value: 'completed', label: 'مكتملة' },
];

export default function MyLessonsPageClient() {
  const userId = useAuthStore((state) => state.user?.id);
  const userBoundary = userId ?? 'pending';
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<LessonFilter>('all');
  const queryFn = useCallback(
    ({ signal }: { signal: AbortSignal }) =>
      studentService.getMyLessons(signal),
    []
  );
  const lessonsQuery = usePlatformQuery<MyLessonDto[]>({
    queryKey: queryKeys.student.lessons(userBoundary),
    queryFn,
    enabled: Boolean(userId),
    staleTime: 30_000,
  });
  const lessons = useMemo(() => lessonsQuery.data ?? [], [lessonsQuery.data]);
  const normalizedSearch = search.trim().toLocaleLowerCase('ar');
  const visibleLessons = useMemo(
    () =>
      lessons.filter((lesson) => {
        const matchesFilter =
          filter === 'all' ||
          (filter === 'completed' ? lesson.isCompleted : !lesson.isCompleted);
        const matchesSearch =
          !normalizedSearch ||
          `${lesson.title} ${lesson.packageName} ${lesson.teacherName}`
            .toLocaleLowerCase('ar')
            .includes(normalizedSearch);
        return matchesFilter && matchesSearch;
      }),
    [filter, lessons, normalizedSearch]
  );
  const isLoading =
    !userId || (lessonsQuery.data === undefined && !lessonsQuery.error);

  if (isLoading)
    return (
      <div className="space-y-4 animate-pulse" aria-label="جارٍ تحميل دروسك">
        <div className="h-40 rounded-3xl bg-[var(--admin-card-strong)]" />
        {[1, 2, 3].map((item) => (
          <div
            key={item}
            className="h-28 rounded-2xl bg-[var(--admin-card-strong)]"
          />
        ))}
      </div>
    );

  if (lessonsQuery.error && lessonsQuery.data === undefined)
    return (
      <div
        role="alert"
        className="rounded-3xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-8 text-center"
      >
        <h1 className="text-xl font-black text-[var(--admin-danger)]">
          تعذر تحميل دروسك
        </h1>
        <p className="mt-2 text-sm font-bold text-[var(--admin-muted)]">
          جرّب التحميل مرة أخرى، واشتراكاتك محفوظة كما هي.
        </p>
        <button
          type="button"
          onClick={() => void lessonsQuery.refetch()}
          className="mt-5 min-h-11 rounded-full bg-[var(--admin-primary)] px-6 font-black text-[var(--admin-primary-contrast)]"
        >
          إعادة التحميل
        </button>
      </div>
    );

  const completedCount = lessons.filter((lesson) => lesson.isCompleted).length;
  return (
    <div className="mx-auto w-full max-w-5xl space-y-5" dir="rtl">
      <section className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-7">
        <div className="flex items-start gap-4">
          <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]">
            <BookOpen className="h-6 w-6" aria-hidden="true" />
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-xs font-black text-[var(--admin-primary)]">
              كل اشتراكاتك في مكان واحد
            </p>
            <h1 className="mt-1 text-2xl font-black text-[var(--admin-text)] sm:text-3xl">
              دروسي
            </h1>
            <p className="mt-2 text-sm font-bold leading-6 text-[var(--admin-muted)]">
              افتح أي درس مشترك فيه مباشرة، سواء من باقة سنة أو ترم أو شهر أو
              حصة منفردة.
            </p>
          </div>
        </div>
        {lessons.length > 0 && (
          <div className="mt-5 flex flex-wrap gap-2 text-xs font-black">
            <span className="rounded-full bg-[var(--admin-primary-10)] px-3 py-2 text-[var(--admin-primary)]">
              {lessons.length} درس متاح
            </span>
            <span className="rounded-full bg-[var(--admin-card-strong)] px-3 py-2 text-[var(--admin-text)]">
              {completedCount} مكتمل
            </span>
          </div>
        )}
      </section>

      {lessons.length === 0 ? (
        <section className="rounded-3xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] p-8 text-center">
          <BookOpen
            className="mx-auto h-10 w-10 text-[var(--admin-primary)]"
            aria-hidden="true"
          />
          <h2 className="mt-4 text-xl font-black text-[var(--admin-text)]">
            لا توجد دروس في اشتراكاتك حاليًا
          </h2>
          <p className="mx-auto mt-2 max-w-md text-sm font-bold leading-6 text-[var(--admin-muted)]">
            بعد تفعيل أي باقة أو ترم أو شهر أو حصة، ستظهر الدروس هنا تلقائيًا.
          </p>
          <Link
            href="/student/packages"
            className="mt-5 inline-flex min-h-11 items-center rounded-full bg-[var(--admin-primary)] px-6 font-black text-[var(--admin-primary-contrast)]"
          >
            استعرض باقاتي
          </Link>
        </section>
      ) : (
        <>
          <section className="sticky top-2 z-10 space-y-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/95 p-3 backdrop-blur">
            <label className="flex min-h-12 items-center gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 focus-within:ring-2 focus-within:ring-[var(--admin-primary)]">
              <Search
                className="h-5 w-5 shrink-0 text-[var(--admin-muted)]"
                aria-hidden="true"
              />
              <span className="sr-only">ابحث في دروسك</span>
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="ابحث باسم الدرس أو المدرس أو الباقة"
                className="w-full bg-transparent text-sm font-bold text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)]"
              />
            </label>
            <div
              className="flex gap-2 overflow-x-auto pb-0.5"
              aria-label="تصفية الدروس"
            >
              {FILTERS.map((item) => (
                <button
                  key={item.value}
                  type="button"
                  onClick={() => setFilter(item.value)}
                  aria-pressed={filter === item.value}
                  className={`min-h-11 shrink-0 rounded-full px-4 text-sm font-black transition-colors ${filter === item.value ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-card-strong)] text-[var(--admin-text)]'}`}
                >
                  {item.label}
                </button>
              ))}
            </div>
          </section>
          {visibleLessons.length === 0 ? (
            <p className="rounded-2xl bg-[var(--admin-card)] p-7 text-center font-bold text-[var(--admin-muted)]">
              لا توجد دروس مطابقة للبحث.
            </p>
          ) : (
            <div className="space-y-3">
              {visibleLessons.map((lesson) => (
                <Link
                  key={lesson.id}
                  href={`/student/packages/${lesson.packageId}/lessons/${lesson.id}`}
                  className="group flex min-h-28 items-center gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 transition hover:border-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] sm:p-4"
                >
                  <div className="relative h-20 w-20 shrink-0 overflow-hidden rounded-xl bg-[var(--admin-card-strong)] sm:h-24 sm:w-28">
                    {lesson.imageUrl ? (
                      <>
                        {/* eslint-disable-next-line @next/next/no-img-element */}
                        <img
                          src={resolveMediaUrl(lesson.imageUrl)}
                          alt=""
                          className="h-full w-full object-cover"
                        />
                      </>
                    ) : (
                      <div className="flex h-full w-full items-center justify-center text-[var(--admin-primary)]">
                        <BookOpen className="h-7 w-7" />
                      </div>
                    )}
                    <span className="absolute bottom-1 end-1 flex h-7 w-7 items-center justify-center rounded-full bg-[var(--admin-sidebar)] text-[var(--admin-primary)] shadow-sm">
                      {lesson.isCompleted ? (
                        <CheckCircle2 className="h-4 w-4" />
                      ) : (
                        <CirclePlay className="h-4 w-4" />
                      )}
                    </span>
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-xs font-black text-[var(--admin-primary)]">
                      {lesson.packageName}
                    </p>
                    <h2 className="mt-1 line-clamp-2 text-base font-black leading-6 text-[var(--admin-text)] sm:text-lg">
                      {lesson.title}
                    </h2>
                    <p className="mt-1 truncate text-xs font-bold text-[var(--admin-muted)]">
                      {lesson.teacherName} · {lesson.sectionTitle}
                    </p>
                    <p className="mt-2 text-xs font-black text-[var(--admin-muted)]">
                      {lesson.videoCount} فيديو ·{' '}
                      {lesson.isCompleted ? 'مكتمل' : 'متاح للمشاهدة'}
                    </p>
                  </div>
                  <ChevronLeft
                    className="h-5 w-5 shrink-0 text-[var(--admin-primary)] transition-transform group-hover:-translate-x-1"
                    aria-hidden="true"
                  />
                </Link>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}

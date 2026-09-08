'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Award, BookOpen, ChevronLeft, Search } from 'lucide-react';
import type { MyLessonDto } from '@/services/student-service';
import { learningSummary } from '@/lib/student-learning-progress';
import { LearningProgress } from './LearningProgress';
import { LastLessonProgress } from './LastLessonProgress';

function CompactCourseCard({ lessons }: { lessons: MyLessonDto[] }) {
  const course = lessons[0];
  const progress = learningSummary(lessons);
  const destination = `/student/packages/${course.packageId}`;

  return (
    <Link href={destination} className="flex min-h-28 items-center gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]">
      <div className="flex min-h-24 w-24 shrink-0 items-center justify-center rounded-xl bg-[#087575] p-3 text-center text-base font-black text-white">
        <span className="line-clamp-3">{course.packageName}</span>
      </div>
      <div className="min-w-0 flex-1 space-y-2">
        <h3 className="line-clamp-2 text-base font-black text-[var(--admin-text)]">{course.packageName}</h3>
        <p className="text-sm text-[var(--admin-muted)]">{course.teacherName}</p>
        <LearningProgress percent={progress.percent} label="تقدّمك" />
      </div>
      <ChevronLeft className="h-5 w-5 shrink-0" aria-hidden="true" />
    </Link>
  );
}

function FeaturedCourseCard({ lessons }: { lessons: MyLessonDto[] }) {
  const course = lessons[0];
  const progress = learningSummary(lessons);
  const destination = `/student/packages/${course.packageId}`;
  return (
    <article className="overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
      <div className="relative flex min-h-36 items-center overflow-hidden bg-[#0A1D3D] px-5 py-6 text-white sm:min-h-44">
        <div className="relative z-10 max-w-[85%]">
          <h3 className="text-3xl font-black leading-snug">{course.packageName}</h3>
          <p className="mt-2 text-sm text-white/90">{course.termTitle}</p>
        </div>
        <div aria-hidden="true" className="absolute bottom-5 end-5 flex items-end gap-1.5 opacity-70">
          <span className="h-3 w-6 bg-[#0E8F8F]" /><span className="mb-3 h-3 w-6 bg-[#0E8F8F]" /><span className="mb-6 h-3 w-6 bg-[#0E8F8F]" />
        </div>
      </div>
      <div className="space-y-3 p-4">
        <p className="text-lg font-bold text-[var(--admin-text)]">{course.teacherName}</p>
        <LearningProgress percent={progress.percent} label="تقدّم المشاهدة" />
        <p className="text-sm text-[var(--admin-muted)]">{progress.completedLessons} من {lessons.length} حصص متاحة مكتملة</p>
        <Link href={destination} className="flex min-h-12 items-center justify-center rounded-xl bg-[var(--admin-primary)] px-4 py-3 text-base font-bold text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] focus-visible:ring-offset-2">افتح الكورس</Link>
      </div>
    </article>
  );
}

export function StudentLearningOverview({ lessons }: { lessons: MyLessonDto[] }) {
  const [filter, setFilter] = useState('all');
  const [search, setSearch] = useState('');
  const summary = learningSummary(lessons);
  const courses = Object.values(lessons.reduce<Record<string, MyLessonDto[]>>((groups, lesson) => {
    (groups[lesson.packageId] ??= []).push(lesson);
    return groups;
  }, {}));
  const completedCourses = courses.filter(course => course.every(lesson => lesson.isCompleted)).length;
  const visibleCourses = courses.filter(course => {
    const complete = course.every(lesson => lesson.isCompleted);
    return (filter === 'all' || (filter === 'completed' ? complete : !complete))
      && course.some(lesson => `${lesson.packageName} ${lesson.teacherName} ${lesson.title}`.toLocaleLowerCase('ar').includes(search.trim().toLocaleLowerCase('ar')));
  });
  const lastLesson = lessons.filter(lesson => lesson.lastWatchedAt)
    .sort((left, right) => Date.parse(right.lastWatchedAt!) - Date.parse(left.lastWatchedAt!))[0];

  return <div className="space-y-7" data-testid="student-learning-overview">
    <section aria-labelledby="my-courses" className="space-y-4">
      <label className="flex min-h-12 items-center gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 focus-within:ring-2 focus-within:ring-[var(--admin-accent)]">
        <Search className="h-5 w-5 shrink-0 text-[var(--admin-muted)]" aria-hidden="true" />
        <input aria-label="ابحث عن كورس أو درس" placeholder="ابحث عن كورس أو درس" value={search} onChange={event => setSearch(event.target.value)} className="min-w-0 flex-1 bg-transparent py-3 text-base text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)]" />
      </label>
      <div className="flex gap-2" aria-label="تصفية الكورسات">
        {[['all', 'الكل'], ['current', 'بكملها'], ['completed', 'مكتملة']].map(([key, label]) => <button key={key} type="button" aria-pressed={filter === key} onClick={() => setFilter(key)} className={`min-h-12 flex-1 rounded-xl px-3 text-base font-bold focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] ${filter === key ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-card-soft)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}>{label}</button>)}
      </div>
      <h2 id="my-courses" className="pt-1 text-xl font-black text-[var(--admin-text)]">الكورسات اللي مشترك فيها</h2>
      {visibleCourses.length === 0 ? <div className="rounded-xl border border-dashed border-[var(--admin-border)] p-5 text-[var(--admin-muted)]" role="status">
        <p>{courses.length === 0 ? 'لسه مفيش كورسات متاحة. فعّل كود اشتراكك أو اختار كورس تبدأه.' : 'مفيش كورسات مطابقة. جرّب فلتر أو كلمة بحث تانية.'}</p>
        {courses.length === 0 && <Link href="/student/packages" className="mt-3 inline-flex min-h-11 items-center font-bold text-[var(--admin-primary)]">استعرض الكورسات</Link>}
      </div> : <div className="grid items-start gap-4 lg:grid-cols-2">
        {visibleCourses.map((course, index) => index === 0
          ? <FeaturedCourseCard key={course[0].packageId} lessons={course} />
          : <CompactCourseCard key={course[0].packageId} lessons={course} />)}
      </div>}
    </section>
    {lastLesson && <LastLessonProgress lesson={lastLesson} />}
    <section aria-label="إحصائيات تقدّمك" className="space-y-4 border-t border-[var(--admin-border)] pt-5">
      <dl className="grid grid-cols-2 gap-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
        <div className="flex items-center justify-center gap-3"><BookOpen className="h-7 w-7 text-[var(--admin-accent)]" aria-hidden="true" /><div><dd className="text-2xl font-black">{courses.length - completedCourses}</dd><dt className="text-sm text-[var(--admin-muted)]">كورسات حالية</dt></div></div>
        <div className="flex items-center justify-center gap-3"><Award className="h-7 w-7 text-[#8A6500]" aria-hidden="true" /><div><dd className="text-2xl font-black">{summary.completedLessons}</dd><dt className="text-sm text-[var(--admin-muted)]">حصص مكتملة</dt></div></div>
      </dl>
      <LearningProgress percent={summary.percent} label="إجمالي تقدّم المشاهدة" />
      <p className="text-sm leading-6 text-[var(--admin-muted)]">{summary.completedVideos} من {summary.totalVideos} فيديو مكتمل. التقدّم حسب وقت المشاهدة مع مراعاة السرعة، للمحتوى المتاح في اشتراكك.</p>
    </section>
  </div>;
}

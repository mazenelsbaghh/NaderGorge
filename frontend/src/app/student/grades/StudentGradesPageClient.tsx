'use client';

import { useCallback, useState } from 'react';
import Link from 'next/link';
import { usePlatformQuery } from '@/components/providers/QueryProvider';
import { queryKeys } from '@/lib/query-keys';
import { formatCairoDateTime } from '@/lib/cairo-time';
import { useAuthStore } from '@/stores/auth-store';
import { studentService, type StudentGradeKind, type StudentGradesDto } from '@/services/student-service';

const filters: { kind: StudentGradeKind; label: string }[] = [
  { kind: 'all', label: 'الكل' }, { kind: 'exam', label: 'الامتحانات' }, { kind: 'homework', label: 'الواجبات' },
];
const number = new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 });
const statuses = { Graded: 'تم التصحيح', PendingReview: 'قيد التصحيح', Missed: 'لم يُسلّم في الموعد' };

export default function StudentGradesPageClient() {
  const userId = useAuthStore(state => state.user?.id);
  const [kind, setKind] = useState<StudentGradeKind>('all');
  const [page, setPage] = useState(1);
  const queryFn = useCallback(({ signal }: { signal: AbortSignal }) => studentService.getGrades(kind, page, signal), [kind, page]);
  const grades = usePlatformQuery<StudentGradesDto>({
    queryKey: queryKeys.student.grades(userId ?? 'pending', kind, page), queryFn, enabled: Boolean(userId), staleTime: 0,
  });
  const loading = !userId || (!grades.data && !grades.error);
  return (
    <div className="space-y-6 text-[var(--admin-text)]" dir="rtl">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div><h1 className="text-3xl font-black">درجاتي</h1>
          <p className="mt-2 max-w-2xl text-sm leading-7 text-[var(--admin-muted)]">نتائج امتحاناتك وواجباتك في مكان واحد. كل محاولة ظاهرة بتاريخها، والدرجة تظهر بعد اكتمال التصحيح.</p></div>
        <button type="button" onClick={() => void grades.refetch()} disabled={loading}
          className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 font-bold hover:bg-[var(--admin-hover)] disabled:opacity-50">تحديث النتائج</button>
      </header>
      <div role="group" aria-label="نوع النتائج" className="flex flex-wrap gap-2">
        {filters.map(filter => <button key={filter.kind} type="button" aria-pressed={kind === filter.kind}
          onClick={() => { setKind(filter.kind); setPage(1); }}
          className={`min-h-11 rounded-xl border px-5 font-bold ${kind === filter.kind ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] hover:bg-[var(--admin-hover)]'}`}>{filter.label}</button>)}
      </div>
      {grades.error ? <section role="alert" className="rounded-xl border border-[var(--admin-border)] p-6">
        <h2 className="text-lg font-bold">تعذر تحميل النتائج</h2><p className="mt-2 text-sm">جرّب زر «تحديث النتائج» مرة أخرى.</p>
      </section> : loading ? <div role="status" aria-label="جارٍ تحميل النتائج" className="space-y-3 motion-safe:animate-pulse">
        {[1, 2, 3].map(row => <div key={row} className="h-28 rounded-xl bg-[var(--admin-card-strong)]" />)}
      </div> : grades.data && <>
        <p role="status" className="text-sm text-[var(--admin-muted)]">{number.format(grades.data.totalCount)} نتيجة · الأحدث أولًا</p>
        {grades.data.items.length === 0 ? <section className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8">
          <h2 className="text-xl font-bold">لا توجد نتائج هنا حتى الآن</h2>
          <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">بعد تسليم امتحان أو واجب، ستجد نتيجته وحالة تصحيحه هنا.</p>
          <Link href="/student/lessons" className="mt-4 inline-flex min-h-11 items-center font-bold text-[var(--admin-primary)]">العودة إلى دروسي</Link>
        </section> : <ol aria-label="سجل الدرجات" className="divide-y divide-[var(--admin-border)] rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
          {grades.data.items.map(grade => <li key={`${grade.kind}-${grade.id}`} className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0 space-y-1">
              <p className="text-sm text-[var(--admin-muted)]">{grade.kind === 'exam' ? 'امتحان' : 'واجب'} · <time dateTime={grade.attemptedAt}>{formatCairoDateTime(grade.attemptedAt, { day: 'numeric', month: 'long', year: 'numeric' })}</time></p>
              <h2 className="break-words text-lg font-bold">{grade.title}</h2>
              {grade.lessonTitle && <p className="break-words text-sm text-[var(--admin-muted)]">{grade.lessonTitle}</p>}
            </div>
            <div className="shrink-0 sm:text-left">
              {grade.status === 'Graded' && grade.score !== null && <p className="text-xl font-black tabular-nums" aria-label="الدرجة">{number.format(grade.score)} <span className="text-sm font-normal text-[var(--admin-muted)]">من {number.format(grade.totalScore)}</span></p>}
              <p className={`mt-1 text-sm font-bold ${grade.status === 'Graded' ? 'text-[var(--admin-primary)]' : 'text-[var(--admin-muted)]'}`}>{statuses[grade.status]}</p>
            </div>
          </li>)}
        </ol>}
        {grades.data.totalCount > grades.data.pageSize && <nav aria-label="صفحات النتائج" className="flex items-center justify-between gap-3">
          <button type="button" disabled={page === 1} onClick={() => setPage(page - 1)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 disabled:opacity-40">السابق</button>
          <span className="text-sm">صفحة {number.format(page)} من {number.format(Math.ceil(grades.data.totalCount / grades.data.pageSize))}</span>
          <button type="button" disabled={page * grades.data.pageSize >= grades.data.totalCount} onClick={() => setPage(page + 1)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 disabled:opacity-40">التالي</button>
        </nav>}
      </>}
    </div>
  );
}

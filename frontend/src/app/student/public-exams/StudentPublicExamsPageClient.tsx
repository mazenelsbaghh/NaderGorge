'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ClipboardList, GraduationCap, LockKeyhole, RefreshCcw, Trophy } from 'lucide-react';
import { PurchaseContentModal } from '@/components/balance/PurchaseContentModal';
import { publicExamsService } from '@/services/public-exams-service';
import type { PublicExamProductDto } from '@/services/admin-sales-service';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { getGradeLevelLabel } from '@/lib/academic-labels';

export default function StudentPublicExamsPageClient() {
  const router = useRouter();
  const [exams, setExams] = useState<PublicExamProductDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [purchaseExam, setPurchaseExam] = useState<PublicExamProductDto | null>(null);
  const [gradeFilter, setGradeFilter] = useState('all');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setExams(await publicExamsService.list());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'تعذر تحميل الامتحانات العامة.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const cleanupCacheStore = registerCacheStore('public-exams', () => {}, () => void load());
    void load();
    return cleanupCacheStore;
  }, [load]);

  const grades = useMemo(() => Array.from(new Set(exams.map((exam) => exam.gradeLevel).filter(Boolean))) as string[], [exams]);
  const filteredExams = gradeFilter === 'all' ? exams : exams.filter((exam) => exam.gradeLevel === gradeFilter);
  const examHref = (examId: string) => `/student/exams/${examId}?from=public-exams`;

  return (
    <main className="mx-auto max-w-6xl space-y-6 pb-16" dir="rtl">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-black text-[var(--admin-text)]">الامتحانات العامة</h1>
          <p className="mt-1 text-sm font-medium text-[var(--admin-muted)]">امتحانات مستقلة عن الحصص، منها المجاني والمدفوع حسب صلاحية كل امتحان.</p>
        </div>
        <button onClick={load} disabled={loading} className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-card-soft)] disabled:opacity-60">
          <RefreshCcw className="h-4 w-4" />
          تحديث
        </button>
      </div>

      {grades.length > 0 ? (
        <div className="flex flex-wrap gap-2">
          <button onClick={() => setGradeFilter('all')} className={filterClass(gradeFilter === 'all')}>كل الصفوف</button>
          {grades.map((grade) => (
            <button key={grade} onClick={() => setGradeFilter(grade)} className={filterClass(gradeFilter === grade)}>{getGradeLevelLabel(grade)}</button>
          ))}
        </div>
      ) : null}

      {error ? <div className="rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 text-sm font-bold text-[var(--admin-danger)]">{error}</div> : null}

      {loading ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {[1, 2, 3].map((item) => <div key={item} className="h-44 animate-pulse rounded-2xl bg-[var(--admin-card-soft)]" />)}
        </div>
      ) : filteredExams.length === 0 ? (
        <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 text-center">
          <ClipboardList className="mx-auto mb-3 h-10 w-10 text-[var(--admin-muted)]" />
          <p className="text-sm font-bold text-[var(--admin-muted)]">لا توجد امتحانات عامة متاحة لبياناتك الدراسية حالياً.</p>
          <p className="mx-auto mt-2 max-w-md text-xs font-medium leading-6 text-[var(--admin-muted)]">
            الامتحانات غير المطابقة لمرحلتك أو صفك أو موادك لا تظهر هنا.
          </p>
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {filteredExams.map((exam) => {
            const hasAccess = exam.hasAccess ?? !exam.isPaid;
            const hasCompletedAttempt = exam.hasCompletedAttempt === true;
            const actionLabel = hasCompletedAttempt ? 'النتيجة' : 'دخول';
            const ActionIcon = hasCompletedAttempt ? Trophy : GraduationCap;

            return (
              <article key={exam.id} className="flex min-h-52 flex-col justify-between rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
                <div className="space-y-3">
                  <div className="flex items-start justify-between gap-3">
                    <div className="rounded-2xl bg-[var(--admin-primary-15)] p-3 text-[var(--admin-primary)]">
                      <GraduationCap className="h-6 w-6" />
                    </div>
                    <div className="flex flex-col items-end gap-2">
                      <span className="rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-black text-[var(--admin-muted)]">{exam.isPaid ? `${exam.price} ج.م` : 'مجاني'}</span>
                      {hasAccess ? (
                        <span className="rounded-full bg-emerald-500/10 px-3 py-1 text-xs font-black text-emerald-700 dark:text-emerald-300">متاح لك</span>
                      ) : null}
                    </div>
                  </div>
                  <div>
                    <h2 className="text-lg font-black text-[var(--admin-text)]">{exam.examTitle}</h2>
                    <p className="mt-1 text-sm font-medium text-[var(--admin-muted)]">{[exam.gradeLevel ? getGradeLevelLabel(exam.gradeLevel) : null, exam.isPlatformWide ? 'عام للمنصة' : null].filter(Boolean).join(' - ') || 'امتحان عام'}</p>
                  </div>
                </div>

                <div className="mt-5">
                  {hasAccess ? (
                    <button onClick={() => router.push(examHref(exam.examId))} className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-full bg-[var(--admin-primary)] px-4 py-2 text-sm font-black text-[var(--admin-primary-contrast)]">
                      <ActionIcon className="h-4 w-4" />
                      {actionLabel}
                    </button>
                  ) : (
                    <button onClick={() => setPurchaseExam(exam)} className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-full bg-[var(--admin-primary)] px-4 py-2 text-sm font-black text-[var(--admin-primary-contrast)]">
                      <LockKeyhole className="h-4 w-4" />
                      شراء الامتحان
                    </button>
                  )}
                </div>
              </article>
            );
          })}
        </div>
      )}

      {purchaseExam ? (
        <PurchaseContentModal
          isOpen={Boolean(purchaseExam)}
          onClose={() => setPurchaseExam(null)}
          onPurchaseSuccess={async () => {
            await load();
            router.push(examHref(purchaseExam.examId));
          }}
          contentType="Exam"
          contentId={purchaseExam.id}
          contentName={purchaseExam.examTitle}
          price={purchaseExam.price}
        />
      ) : null}
    </main>
  );
}

function filterClass(active: boolean) {
  return `rounded-full px-4 py-2 text-sm font-bold transition ${active ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'}`;
}

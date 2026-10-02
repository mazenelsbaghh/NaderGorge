'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Search } from 'lucide-react';
import { adminService, type ExamGradeMessageList } from '@/services/admin-service';
import { getApiErrorSummary } from '@/lib/api-errors';
import { ExamParentMessagesPanel } from './ExamParentMessagesPanel';

const ignoreAttemptStates = () => {};

export function ExamGradeMessagesSettings() {
  const [searchInput, setSearchInput] = useState('');
  const [query, setQuery] = useState({ search: '', page: 1, refresh: 0 });
  const [catalog, setCatalog] = useState<ExamGradeMessageList | null>(null);
  const [selectedId, setSelectedId] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    adminService.listExamGradeMessages(query.search, query.page, controller.signal).then(data => {
      if (controller.signal.aborted) return;
      setCatalog(data);
      setSelectedId(previous => data.items.some(exam => exam.examId === previous) ? previous : data.items[0]?.examId ?? '');
    }).catch(cause => {
      if (!controller.signal.aborted) setError(getApiErrorSummary(cause, 'تعذر تحميل الامتحانات.'));
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [query]);

  const selected = catalog?.items.find(exam => exam.examId === selectedId);
  const totalPages = catalog ? Math.max(1, Math.ceil(catalog.totalCount / catalog.pageSize)) : 1;

  return <section className="space-y-5" dir="rtl" aria-labelledby="exam-grade-settings-title">
    <div className="space-y-2">
      <h2 id="exam-grade-settings-title" className="text-xl font-bold text-[var(--admin-text)]">الامتحانات — إرسال الدرجات</h2>
      <p className="text-sm leading-6 text-[var(--admin-muted)]">اختَر الامتحان لمتابعة رسائل درجات الطلاب على واتساب وإرسال الرسائل الناقصة لولي الأمر.</p>
    </div>
    <form className="flex flex-wrap gap-2" onSubmit={event => {
      event.preventDefault();
      setQuery(previous => ({ search: searchInput.trim(), page: 1, refresh: previous.refresh + 1 }));
    }}>
      <label htmlFor="exam-grade-search" className="sr-only">اسم الامتحان أو المدرس</label>
      <input id="exam-grade-search" value={searchInput} onChange={event => setSearchInput(event.target.value)}
        maxLength={160} placeholder="ابحث باسم الامتحان أو المدرس"
        className="min-h-11 min-w-0 flex-1 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)]" />
      <button type="submit" disabled={loading} className="admin-btn-ghost min-h-11"><Search size={16} aria-hidden="true" />بحث</button>
    </form>
    {loading && <p role="status" className="text-sm text-[var(--admin-muted)]">جاري تحميل الامتحانات…</p>}
    {error && <div role="alert" className="flex flex-wrap items-center gap-3 text-sm text-[var(--admin-danger)]">
      <p>{error}</p>
      <button type="button" className="admin-btn-ghost min-h-11" onClick={() => setQuery(previous => ({ ...previous, refresh: previous.refresh + 1 }))}>إعادة المحاولة</button>
    </div>}
    {catalog && !loading && !error && <>
      {catalog.items.length === 0 ? <p className="rounded-xl bg-[var(--admin-card-soft)] p-5 text-sm text-[var(--admin-muted)]">لا توجد امتحانات تطابق البحث.</p> : <>
        <div className="max-h-72 overflow-y-auto rounded-xl border border-[var(--admin-border)]" aria-label="اختيار الامتحان">
          {catalog.items.map(exam => <button key={exam.examId} type="button" aria-pressed={selectedId === exam.examId}
            aria-label={`عرض رسائل ${exam.title} — ${exam.teacherName}`} onClick={() => setSelectedId(exam.examId)}
            className="flex min-h-16 w-full items-center justify-between gap-3 border-b border-[var(--admin-border)] px-4 py-3 text-right last:border-b-0 hover:bg-[var(--admin-card-soft)] aria-pressed:bg-[var(--admin-primary-15)]">
            <span className="min-w-0 space-y-1">
              <span className="block break-words font-bold text-[var(--admin-text)]">{exam.title}</span>
              <span className="block text-xs text-[var(--admin-muted)]">{exam.teacherName} · {new Date(exam.createdAt).toLocaleDateString('ar-EG')}</span>
            </span>
            <span className="shrink-0 text-xs text-[var(--admin-muted)]">{exam.finalResultCount} نتيجة جاهزة</span>
          </button>)}
        </div>
        <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-[var(--admin-muted)]">
          <p>{catalog.totalCount} امتحان · صفحة {catalog.page} من {totalPages}</p>
          <div className="flex gap-2">
            <button type="button" disabled={catalog.page <= 1} className="admin-btn-ghost min-h-11"
              onClick={() => setQuery(previous => ({ ...previous, page: catalog.page - 1 }))}>السابق</button>
            <button type="button" disabled={catalog.page >= totalPages} className="admin-btn-ghost min-h-11"
              onClick={() => setQuery(previous => ({ ...previous, page: catalog.page + 1 }))}>التالي</button>
          </div>
        </div>
      </>}
    </>}
    {selected && <div className="space-y-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="break-words text-lg font-bold text-[var(--admin-text)]">{selected.title}</h3>
          <p className="text-sm text-[var(--admin-muted)]">{selected.teacherName}</p>
        </div>
        <Link href={`/admin/content/exams/${selected.examId}/dashboard`} className="admin-btn-ghost min-h-11">تفاصيل الطلاب والدرجات</Link>
      </div>
      <ExamParentMessagesPanel key={selected.examId} examId={selected.examId} onStatesChange={ignoreAttemptStates} sendLabel="إرسال الدرجات" />
    </div>}
  </section>;
}

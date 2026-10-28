'use client';

import { useEffect, useRef, useState } from 'react';
import { Download, Search } from 'lucide-react';
import type { ReportAudience } from '@/services/advanced-report-service';
import type { FollowUpOptions } from '@/services/student-follow-up-service';
import { lessonStudentReportService, reportError, type LessonStudentFilter, type LessonStudentReport, type ReportStudent } from '@/services/lesson-student-report-service';
import { LearningStudentPicker } from './LearningStudentPicker';
import { LessonStudentResults } from './LessonStudentResults';

const inputClass = 'mt-2 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-normal text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:opacity-60';
const emptyTeacher = '00000000-0000-0000-0000-000000000000';
interface Selection {
  teacherId: string; packageId: string; lessonId: string; mode: 'lesson' | 'student';
  attendance: LessonStudentFilter['attendance']; student: ReportStudent | null; state: LessonStudentFilter['state'];
}
export function LessonStudentReports({ audience, initialStudent }: { audience: ReportAudience; initialStudent?: ReportStudent }) {
  const [selection, setSelection] = useState<Selection>({ teacherId: '', packageId: '', lessonId: '', mode: initialStudent ? 'student' : 'lesson', attendance: 'All', student: initialStudent ?? null, state: 'All' });
  const [options, setOptions] = useState<FollowUpOptions>({ teachers: [], packages: [], lessons: [] });
  const [optionsLoading, setOptionsLoading] = useState(true);
  const [optionsError, setOptionsError] = useState('');
  const [optionsVersion, setOptionsVersion] = useState(0);
  const [report, setReport] = useState<LessonStudentReport | null>(null);
  const [applied, setApplied] = useState<LessonStudentFilter | null>(null);
  const [busy, setBusy] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState('');
  const request = useRef<AbortController | null>(null);
  const exportVersion = useRef(0);
  const results = useRef<HTMLElement>(null);
  const { teacherId, packageId, lessonId, mode, attendance, student, state } = selection;

  useEffect(() => {
    const controller = new AbortController();
    setOptionsLoading(true); setOptionsError('');
    lessonStudentReportService.options(audience, { teacherId: teacherId || undefined, packageId: packageId || undefined }, controller.signal)
      .then(choices => { if (!controller.signal.aborted) setOptions(choices); })
      .catch(reason => { if (!controller.signal.aborted) setOptionsError(reportError(reason)); })
      .finally(() => { if (!controller.signal.aborted) setOptionsLoading(false); });
    return () => controller.abort();
  }, [audience, teacherId, packageId, optionsVersion]);
  useEffect(() => () => { request.current?.abort(); exportVersion.current++; }, []);

  function change(patch: Partial<Selection>) {
    request.current?.abort(); exportVersion.current++;
    setSelection(current => ({ ...current, ...patch }));
    setReport(null); setApplied(null); setError(''); setBusy(false); setExporting(false);
  }
  async function load(filter: LessonStudentFilter) {
    request.current?.abort(); const controller = new AbortController(); request.current = controller;
    setBusy(true); setError(''); setReport(null); setApplied(null);
    try {
      const nextReport = await lessonStudentReportService.read(audience, filter, controller.signal);
      if (!controller.signal.aborted) { setReport(nextReport); setApplied(filter); }
    } catch (reason) { if (!controller.signal.aborted) setError(reportError(reason)); }
    finally { if (!controller.signal.aborted) setBusy(false); }
  }
  function run() {
    void load({ teacherId: teacherId || emptyTeacher, packageId, lessonIds: lessonId ? [lessonId] : [],
      studentId: mode === 'student' ? student?.id : undefined, attendance: mode === 'lesson' ? attendance : 'All',
      state: mode === 'student' ? state : 'All', minimumDays: 0, search: '', page: 1, pageSize: 20 });
    results.current?.focus();
  }
  async function download() {
    if (!applied) return;
    const version = ++exportVersion.current;
    setExporting(true); setError('');
    try {
      const blob = await lessonStudentReportService.export(audience, applied);
      if (version !== exportVersion.current) return;
      const url = URL.createObjectURL(blob); const link = document.createElement('a');
      link.href = url; link.download = 'تقرير-الحصص-والطلاب.xlsx'; link.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (reason) {
      if (version === exportVersion.current) {
        const payload = (reason as { response?: { data?: unknown } }).response?.data;
        if (payload instanceof Blob) {
          try { setError(JSON.parse(await payload.text()).message || 'تعذر تنزيل التقرير.'); }
          catch { setError('تعذر تنزيل التقرير. أعد المحاولة.'); }
        } else setError(reportError(reason));
      }
    } finally { if (version === exportVersion.current) setExporting(false); }
  }
  const canRun = packageId && !optionsLoading && !optionsError && (audience === 'teacher' || teacherId)
    && (mode === 'lesson' ? lessonId : student);

  return <div className="space-y-6">
    <header><h2 className="text-xl font-bold text-[var(--admin-text)]">تقارير الحصص والطلاب</h2><p className="mt-2 text-sm text-[var(--admin-muted)]">كشف حضور لحصة محددة، أو تفاصيل ما شاهده طالب في الكورس.</p></header>
    <div className="flex flex-wrap gap-2" role="group" aria-label="نوع المتابعة">{([{ key: 'lesson', label: 'حضور حصة' }, { key: 'student', label: 'مشاهدة طالب' }] as const).map(choice => <button key={choice.key} type="button" aria-pressed={mode === choice.key} onClick={() => change({ mode: choice.key, student: null, lessonId: '', attendance: 'All', state: 'All' })}
      className={`min-h-11 rounded-xl border px-4 text-sm font-bold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${mode === choice.key ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-text)]' : 'border-[var(--admin-border)] text-[var(--admin-muted)]'}`}>{choice.label}</button>)}</div>
    <form onSubmit={event => { event.preventDefault(); if (canRun && !busy) run(); }} className="space-y-5 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-6">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {audience === 'admin' && <label className="text-sm font-bold text-[var(--admin-text)]">المدرس<select aria-label="المدرس" value={teacherId} disabled={optionsLoading} className={inputClass} onChange={event => change({ teacherId: event.target.value, packageId: '', lessonId: '', student: null })}><option value="">اختر المدرس</option>{options.teachers.map(teacher => <option key={teacher.id} value={teacher.id}>{teacher.name}</option>)}</select></label>}
        <label className="text-sm font-bold text-[var(--admin-text)]">الكورس<select aria-label="الكورس" value={packageId} disabled={optionsLoading || (audience === 'admin' && !teacherId)} className={inputClass} onChange={event => change({ packageId: event.target.value, lessonId: '', student: initialStudent ?? null })}><option value="">اختر الكورس</option>{options.packages.map(course => <option key={course.id} value={course.id}>{course.name}</option>)}</select></label>
        <label className="text-sm font-bold text-[var(--admin-text)]">الحصة<select aria-label="الحصة" value={lessonId} disabled={!packageId || optionsLoading} className={inputClass} onChange={event => change({ lessonId: event.target.value })}><option value="">{mode === 'student' ? 'كل حصص الكورس المتاحة للطالب' : 'اختر الحصة'}</option>{options.lessons.map(lesson => <option key={lesson.id} value={lesson.id}>{lesson.termTitle} · {lesson.title}</option>)}</select></label>
        {mode === 'lesson' ? <label className="text-sm font-bold text-[var(--admin-text)]">الحضور<select aria-label="الحضور" value={attendance} className={inputClass} onChange={event => change({ attendance: event.target.value as Selection['attendance'] })}><option value="All">الحضور والغياب</option><option value="present">الحاضرون فقط</option><option value="absent">الغائبون فقط</option></select></label> : <>
          <LearningStudentPicker key={`${teacherId}-${packageId}`} audience={audience} teacherId={teacherId || emptyTeacher} packageId={packageId} selected={student} onSelect={selected => change({ student: selected })} />
          <label className="text-sm font-bold text-[var(--admin-text)]">حالة المشاهدة<select aria-label="حالة المشاهدة" value={state} className={inputClass} onChange={event => change({ state: event.target.value as Selection['state'] })}><option value="All">كل الحالات</option><option value="NotStarted">لم يبدأ المشاهدة</option><option value="Started">بدأ ولم يكمل</option><option value="Completed">مكتمل</option></select></label>
        </>}
      </div>
      <p className="text-xs leading-6 text-[var(--admin-muted)]">الحاضر هو من بدأ مشاهدة فيديو واحد على الأقل في الحصة. الغائب لديه صلاحية مشاهدة ولم يُسجّل له بدء مشاهدة. التقرير يشمل المحتوى المتاح للطالب حسب اشتراكه وصفه.</p>
      {optionsLoading && <p role="status" className="text-sm text-[var(--admin-muted)]">جاري تحميل الكورسات والحصص…</p>}
      {packageId && !optionsLoading && !optionsError && options.lessons.length === 0 && <p role="status" className="text-sm text-[var(--admin-muted)]">لا توجد حصص بها فيديوهات مفعّلة في هذا الكورس.</p>}
      {optionsError && <p role="alert" className="text-sm text-[var(--admin-danger)]">{optionsError}<button type="button" onClick={() => setOptionsVersion(current => current + 1)} className="mr-3 min-h-11 underline">إعادة تحميل الاختيارات</button></p>}
      <div className="flex flex-wrap gap-3"><button type="submit" disabled={!canRun || busy} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-bold text-white disabled:opacity-50"><Search className="h-4 w-4" />{busy ? 'جاري عرض التقرير…' : 'عرض التقرير'}</button>
        <button type="button" onClick={() => void download()} disabled={!report?.totalRows || busy || exporting} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-text)] disabled:opacity-50"><Download className="h-4 w-4" />{exporting ? 'جاري تجهيز الإكسيل…' : 'تنزيل التقرير إكسيل'}</button></div>
    </form>
    <section ref={results} tabIndex={-1} aria-label="نتيجة تقرير الحصص والطلاب" className="space-y-4 outline-none">
      {error && <p role="alert" className="rounded-xl border border-[var(--admin-danger)] p-4 text-sm text-[var(--admin-danger)]">{error}<button type="button" disabled={!canRun || busy} onClick={run} className="mr-3 min-h-11 underline disabled:opacity-50">إعادة عرض التقرير</button></p>}
      <LessonStudentResults report={report} busy={busy} mode={mode} onPage={page => { if (applied) void load({ ...applied, page }); }} />
    </section>
  </div>;
}

'use client';

import { useEffect, useRef, useState } from 'react';
import { Download, Search } from 'lucide-react';
import { STAGE_OPTIONS } from '@/lib/academic-labels';
import { lessonStudentReportService } from '@/services/lesson-student-report-service';
import type { FollowUpOptions } from '@/services/student-follow-up-service';
import { exportTeacherCourseSummary, readTeacherCourseSummary, type TeacherSummaryFilter, type TeacherSummaryReport } from '@/services/teacher-course-summary-service';
import { getApiErrorSummary } from '@/lib/api-errors';
import { gradeChoices } from './report-academic-options';
import { TeacherSummaryResults } from './TeacherSummaryResults';

const stageCodes: Record<string, number> = { Secondary: 0, Baccalaureate: 1, Primary: 2, Preparatory: 3, Azhari: 4, American: 5 };
const inputClass = 'mt-2 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-normal text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:opacity-60';

async function summaryError(reason: unknown): Promise<string> {
  const payload = (reason as { response?: { data?: unknown } }).response?.data;
  if (payload instanceof Blob) {
    try { return JSON.parse(await payload.text()).message || 'تعذر تنزيل التقرير. أعد المحاولة.'; }
    catch { return 'تعذر تنزيل التقرير. أعد المحاولة.'; }
  }
  return getApiErrorSummary(reason, 'تعذر تحميل التقرير. أعد المحاولة.');
}

export function TeacherCourseSummary() {
  const [options, setOptions] = useState<FollowUpOptions>({ teachers: [], packages: [], lessons: [] });
  const [stage, setStage] = useState('');
  const [grade, setGrade] = useState('');
  const [course, setCourse] = useState('');
  const [loading, setLoading] = useState(true);
  const [lookupError, setLookupError] = useState('');
  const [version, setVersion] = useState(0);
  const [busy, setBusy] = useState<'read' | 'export' | null>(null);
  const [error, setError] = useState('');
  const [downloaded, setDownloaded] = useState(false);
  const [report, setReport] = useState<TeacherSummaryReport | null>(null);
  const request = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setLookupError('');
    lessonStudentReportService.options('teacher', {}, controller.signal)
      .then(choices => { if (!controller.signal.aborted) setOptions(choices); })
      .catch(reason => { if (!controller.signal.aborted) setLookupError(getApiErrorSummary(reason, 'تعذر تحميل الكورسات. أعد المحاولة.')); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [version]);
  useEffect(() => () => request.current?.abort(), []);
  const courses = options.packages.filter(course => !course.grades?.length || course.grades.some(choice =>
    (stage === '' || choice.stage === Number(stage)) && (grade === '' || choice.grade === Number(grade))));
  const grades = gradeChoices.filter(([stageCode]) => stage === '' || stageCode === Number(stage));
  const selection: TeacherSummaryFilter = { stage: stage === '' ? undefined : Number(stage), grade: grade === '' ? undefined : Number(grade), packageId: course || undefined };
  const blocked = loading || !!lookupError || !!busy;

  function resetReport() {
    request.current?.abort(); setBusy(null); setError(''); setDownloaded(false); setReport(null);
  }
  async function read(page = 1) {
    request.current?.abort(); const controller = new AbortController(); request.current = controller;
    setBusy('read'); setError('');
    try {
      const nextReport = await readTeacherCourseSummary({ ...selection, page }, controller.signal);
      if (!controller.signal.aborted) setReport(nextReport);
    } catch (reason) { if (!controller.signal.aborted) setError(getApiErrorSummary(reason, 'تعذر عرض التقرير. أعد المحاولة.')); }
    finally { if (!controller.signal.aborted) setBusy(null); }
  }
  async function download() {
    request.current?.abort(); const controller = new AbortController(); request.current = controller;
    setBusy('export'); setError(''); setDownloaded(false);
    try {
      const blob = await exportTeacherCourseSummary(selection, controller.signal);
      if (controller.signal.aborted) return;
      const url = URL.createObjectURL(blob); const link = document.createElement('a');
      link.href = url; link.download = 'ملخص-الطلاب.xlsx'; document.body.appendChild(link); link.click(); link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000); setDownloaded(true);
    } catch (reason) {
      const description = await summaryError(reason);
      if (!controller.signal.aborted) setError(description);
    } finally { if (!controller.signal.aborted) setBusy(null); }
  }
  return <div className="min-w-0 space-y-6" dir="rtl">
    <header><h2 className="text-xl font-bold text-[var(--admin-text)]">ملخص الطلاب</h2><p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">كل طلاب كورساتك في تقرير واحد. يمكنك تحديد مرحلة أو صف أو كورس، أو عرض الكل.</p></header>
    <form onSubmit={event => { event.preventDefault(); if (!blocked) void read(); }} className="space-y-5 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-6">
      <div className="grid gap-4 sm:grid-cols-3">
        <label className="text-sm font-bold text-[var(--admin-text)]">المرحلة<select aria-label="المرحلة" value={stage} disabled={loading || !!lookupError} className={inputClass} onChange={event => { resetReport(); setStage(event.target.value); setGrade(''); setCourse(''); }}><option value="">كل المراحل</option>{STAGE_OPTIONS.map(choice => <option key={choice.value} value={stageCodes[choice.value]}>{choice.label}</option>)}</select></label>
        <label className="text-sm font-bold text-[var(--admin-text)]">الصف<select aria-label="الصف" value={grade} disabled={loading || !!lookupError} className={inputClass} onChange={event => { resetReport(); setGrade(event.target.value); setCourse(''); }}><option value="">كل الصفوف</option>{grades.map(([, gradeCode, label]) => <option key={gradeCode} value={gradeCode}>{label}</option>)}</select></label>
        <label className="text-sm font-bold text-[var(--admin-text)]">الكورس<select aria-label="الكورس" value={course} disabled={loading || !!lookupError} className={inputClass} onChange={event => { resetReport(); setCourse(event.target.value); }}><option value="">كل الكورسات</option>{courses.map(course => <option key={course.id} value={course.id}>{course.name}</option>)}</select></label>
      </div>
      <p className="text-sm leading-7 text-[var(--admin-muted)]">صف لكل طالب ببياناته المسجلة، ومنها المحافظة والعنوان والمدرسة وأرقام التواصل. أمام كل حصة: المشاهدة ودرجة الامتحان والواجب، وحالة التصحيح.</p>
      {loading && <p role="status" className="text-sm text-[var(--admin-muted)]">جاري تحميل الكورسات…</p>}
      {!loading && !lookupError && options.packages.length === 0 && <p role="status" className="text-sm text-[var(--admin-muted)]">لا توجد كورسات متاحة في نطاقك.</p>}
      {lookupError && <p role="alert" className="text-sm text-[var(--admin-danger)]">{lookupError}<button type="button" onClick={() => setVersion(current => current + 1)} className="mr-3 min-h-11 underline">إعادة تحميل الكورسات</button></p>}
      {error && <p role="alert" className="text-sm text-[var(--admin-danger)]">{error}</p>}
      <div className="flex flex-wrap gap-3"><button type="submit" disabled={blocked} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-bold text-white disabled:opacity-50"><Search className="h-4 w-4" />{busy === 'read' ? 'جاري عرض التقرير…' : 'عرض التقرير'}</button>
        <button type="button" onClick={() => void download()} disabled={blocked} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] px-5 text-sm font-bold text-[var(--admin-text)] disabled:opacity-50"><Download className="h-4 w-4" />{busy === 'export' ? 'جاري تجهيز الملخص…' : 'تنزيل الداتا إكسيل'}</button></div>
      {downloaded && <p role="status" className="text-sm font-bold text-[var(--admin-text)]">تم تنزيل ملخص الطلاب.</p>}
    </form>
    {report && <TeacherSummaryResults report={report} busy={!!busy} onPage={page => void read(page)} />}
  </div>;
}

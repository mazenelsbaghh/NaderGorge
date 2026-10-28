'use client';

import { useEffect, useRef, useState } from 'react';
import { Download } from 'lucide-react';
import { STAGE_OPTIONS } from '@/lib/academic-labels';
import { lessonStudentReportService } from '@/services/lesson-student-report-service';
import type { FollowUpOptions } from '@/services/student-follow-up-service';
import { exportTeacherCourseSummary } from '@/services/teacher-course-summary-service';
import { getApiErrorSummary } from '@/lib/api-errors';

const stageCodes: Record<string, number> = { Secondary: 0, Baccalaureate: 1, Primary: 2, Preparatory: 3, Azhari: 4, American: 5 };
const inputClass = 'mt-2 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-normal text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:opacity-60';

export function TeacherCourseSummary() {
  const [options, setOptions] = useState<FollowUpOptions>({ teachers: [], packages: [], lessons: [] });
  const [stage, setStage] = useState('');
  const [course, setCourse] = useState('');
  const [loading, setLoading] = useState(true);
  const [lookupError, setLookupError] = useState('');
  const [version, setVersion] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [downloaded, setDownloaded] = useState(false);
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
  const stages = STAGE_OPTIONS.filter(choice => options.packages.some(course => !course.grades?.length || course.grades.some(grade => grade.stage === stageCodes[choice.value])));
  const courses = options.packages.filter(course => !course.grades?.length || course.grades.some(grade => grade.stage === Number(stage)));
  function resetExport() { request.current?.abort(); setBusy(false); setError(''); setDownloaded(false); }
  async function download() {
    if (!stage || !course || busy) return;
    const controller = new AbortController(); request.current = controller;
    setBusy(true); setError(''); setDownloaded(false);
    try {
      const blob = await exportTeacherCourseSummary({ stage: Number(stage), packageId: course }, controller.signal);
      if (controller.signal.aborted) return;
      const url = URL.createObjectURL(blob); const link = document.createElement('a');
      link.href = url; link.download = 'ملخص-الكورس.xlsx'; document.body.appendChild(link); link.click(); link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000); setDownloaded(true);
    } catch (reason) {
      if (!controller.signal.aborted) {
        const payload = (reason as { response?: { data?: unknown } }).response?.data;
        let description = 'تعذر تنزيل الملخص. أعد المحاولة.';
        if (payload instanceof Blob) { try { description = JSON.parse(await payload.text()).message || description; } catch { /* Keep a readable message for non-JSON transport failures. */ } }
        else description = getApiErrorSummary(reason, description);
        if (!controller.signal.aborted) setError(description);
      }
    } finally { if (!controller.signal.aborted) setBusy(false); }
  }
  return <div className="space-y-6" dir="rtl">
    <header><h2 className="text-xl font-bold text-[var(--admin-text)]">ملخص</h2><p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">اختار المرحلة والكورس، ونزّل متابعة الطلاب في ملف إكسيل واحد.</p></header>
    <form onSubmit={event => { event.preventDefault(); void download(); }} className="max-w-3xl space-y-5 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-6">
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="text-sm font-bold text-[var(--admin-text)]">المرحلة<select aria-label="المرحلة" value={stage} disabled={loading || !!lookupError} className={inputClass} onChange={event => { resetExport(); setStage(event.target.value); setCourse(''); }}><option value="">اختر المرحلة</option>{stages.map(choice => <option key={choice.value} value={stageCodes[choice.value]}>{choice.label}</option>)}</select></label>
        <label className="text-sm font-bold text-[var(--admin-text)]">الكورس<select aria-label="الكورس" value={course} disabled={!stage || loading || !!lookupError} className={inputClass} onChange={event => { resetExport(); setCourse(event.target.value); }}><option value="">اختر الكورس</option>{courses.map(course => <option key={course.id} value={course.id}>{course.name}</option>)}</select></label>
      </div>
      <p className="text-sm leading-7 text-[var(--admin-muted)]">صف لكل طالب، وأمام كل حصة: شاهد أو لم يشاهد، ودرجة الامتحان، ودرجة الواجب. الدرجات قيد التصحيح تظهر بحالتها، ولو مفيش امتحان أو واجب هتلاقي ده موضح.</p>
      {loading && <p role="status" className="text-sm text-[var(--admin-muted)]">جاري تحميل الكورسات…</p>}
      {!loading && !lookupError && options.packages.length === 0 && <p role="status" className="text-sm text-[var(--admin-muted)]">لا توجد كورسات متاحة للتصدير في نطاقك.</p>}
      {lookupError && <p role="alert" className="text-sm text-[var(--admin-danger)]">{lookupError}<button type="button" onClick={() => setVersion(current => current + 1)} className="mr-3 min-h-11 underline">إعادة تحميل الكورسات</button></p>}
      {error && <p role="alert" className="text-sm text-[var(--admin-danger)]">{error}</p>}
      <button type="submit" disabled={!stage || !course || loading || !!lookupError || busy} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-bold text-white disabled:opacity-50"><Download className="h-4 w-4" />{busy ? 'جاري تجهيز الملخص…' : 'تنزيل الداتا إكسيل'}</button>
      {downloaded && <p role="status" className="text-sm font-bold text-[var(--admin-text)]">تم تنزيل ملخص الكورس.</p>}
    </form>
  </div>;
}

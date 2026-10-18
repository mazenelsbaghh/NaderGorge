'use client';

import { useEffect, useRef, useState } from 'react';
import { Download, RefreshCw, Search } from 'lucide-react';
import { studentFollowUpService, type FollowUpAudience, type FollowUpFilter, type FollowUpOptions, type FollowUpReport } from '@/services/student-follow-up-service';
import { FollowUpResults } from './StudentFollowUpResults';

const input = 'min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:opacity-60';
const emptyOptions: FollowUpOptions = { teachers: [], packages: [], lessons: [] };
const noTeacher = '00000000-0000-0000-0000-000000000000';
const gradeChoices: Array<[number, number, string]> = [
  [0, 0, 'الأول الثانوي'], [0, 1, 'الثاني الثانوي'], [0, 31, 'الثالث الثانوي'],
  [1, 2, 'الأول بكالوريا'], [1, 3, 'الثاني بكالوريا'],
  ...Array.from({ length: 6 }, (_, i): [number, number, string] => [2, i + 10, `الصف ${i + 1} الابتدائي`]),
  ...Array.from({ length: 3 }, (_, i): [number, number, string] => [3, i + 20, `الصف ${i + 1} الإعدادي`]),
  ...Array.from({ length: 6 }, (_, i): [number, number, string] => [4, i + 40, `الصف ${i + 1} الابتدائي الأزهري`]),
  ...Array.from({ length: 3 }, (_, i): [number, number, string] => [4, i + 50, `الصف ${i + 1} الإعدادي الأزهري`]),
  ...Array.from({ length: 3 }, (_, i): [number, number, string] => [4, i + 60, `الصف ${i + 1} الثانوي الأزهري`]),
  ...Array.from({ length: 12 }, (_, i): [number, number, string] => [5, i + 70, `Grade ${i + 1}`]),
];
export function StudentFollowUp({ audience }: { audience: FollowUpAudience }) {
  const [teacher, setTeacher] = useState('');
  const [course, setCourse] = useState('');
  const [grade, setGrade] = useState('');
  const [lessons, setLessons] = useState<string[]>([]);
  const [state, setState] = useState<FollowUpFilter['state']>('NotStarted');
  const [days, setDays] = useState(0);
  const [search, setSearch] = useState('');
  const [options, setOptions] = useState<FollowUpOptions>(emptyOptions);
  const [optionsLoading, setOptionsLoading] = useState(true);
  const [optionsError, setOptionsError] = useState('');
  const [optionsVersion, setOptionsVersion] = useState(0);
  const [applied, setApplied] = useState<FollowUpFilter | null>(null);
  const [report, setReport] = useState<FollowUpReport | null>(null);
  const [busy, setBusy] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState('');
  const queryController = useRef<AbortController | null>(null);
  const latestRequest = useRef(0);
  const resultsRef = useRef<HTMLElement>(null);

  useEffect(() => {
    const controller = new AbortController();
    setOptionsLoading(true); setOptionsError('');
    studentFollowUpService.options(audience, teacher, course, controller.signal)
      .then(data => { if (!controller.signal.aborted) setOptions(data); })
      .catch(() => { if (!controller.signal.aborted) setOptionsError('تعذر تحميل الكورسات والحصص. أعد المحاولة.'); })
      .finally(() => { if (!controller.signal.aborted) setOptionsLoading(false); });
    return () => controller.abort();
  }, [audience, teacher, course, optionsVersion]);
  useEffect(() => () => queryController.current?.abort(), []);

  const selectionKey = JSON.stringify({ teacher, course, grade, lessons, state, days, search: search.trim() });
  const appliedKey = applied && JSON.stringify({ teacher: audience === 'teacher' ? '' : applied.teacherId, course: applied.packageId,
    grade: applied.grade === undefined ? '' : `${applied.stage}:${applied.grade}`, lessons: applied.lessonIds,
    state: applied.state, days: applied.minimumDays, search: applied.search });
  const dirty = applied !== null && selectionKey !== appliedKey;

  function clearResults() {
    queryController.current?.abort(); latestRequest.current += 1;
    setApplied(null); setReport(null); setBusy(false); setError('');
  }
  async function load(filter: FollowUpFilter) {
    queryController.current?.abort(); const controller = new AbortController(); queryController.current = controller;
    const version = ++latestRequest.current;
    setBusy(true); setError('');
    try {
      const value = await studentFollowUpService.read(audience, filter, controller.signal);
      if (version === latestRequest.current && !controller.signal.aborted) { setReport(value); setApplied(filter); }
    } catch (reason) {
      if (!controller.signal.aborted && version === latestRequest.current) setError(message(reason, 'تعذر عرض المتابعة. أعد المحاولة.'));
    } finally { if (version === latestRequest.current) setBusy(false); }
  }
  function run() {
    const [stage, gradeValue] = grade ? grade.split(':').map(Number) : [];
    void load({ teacherId: teacher || noTeacher, packageId: course, lessonIds: lessons, stage, grade: gradeValue,
      state, minimumDays: days, search: search.trim(), page: 1, pageSize: 20 });
    resultsRef.current?.focus();
  }
  async function download() {
    if (!applied || dirty || busy) return;
    setExporting(true); setError('');
    try {
      const blob = await studentFollowUpService.export(audience, applied);
      const url = URL.createObjectURL(blob); const link = document.createElement('a');
      link.href = url; link.download = 'متابعة-الطلاب.xlsx'; document.body.appendChild(link); link.click(); link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (reason) { setError(await exportError(reason)); }
    finally { setExporting(false); }
  }

  return <div dir="rtl" className="space-y-6">
    <section aria-label="فلاتر متابعة الطلاب" className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-6">
      <div className="mb-5"><h2 className="text-lg font-bold text-[var(--admin-text)]">مين محتاج متابعة؟</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">اختار الصف والكورس والحصص، واعرض الطلاب أو نزّل نفس النتيجة إكسيل.</p></div>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {audience === 'admin' && <label className="space-y-2 text-sm font-bold text-[var(--admin-text)]">المدرس<select aria-label="المدرس" className={input} value={teacher} disabled={optionsLoading} onChange={e => { setTeacher(e.target.value); setCourse(''); setLessons([]); clearResults(); }}><option value="">اختر المدرس</option>{options.teachers.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>}
        <label className="space-y-2 text-sm font-bold text-[var(--admin-text)]">الصف<select aria-label="الصف" className={input} value={grade} onChange={e => setGrade(e.target.value)}><option value="">كل الصفوف</option>{gradeChoices.map(([stage, level, title]) => <option key={`${stage}:${level}`} value={`${stage}:${level}`}>{title}</option>)}</select></label>
        <label className="space-y-2 text-sm font-bold text-[var(--admin-text)]">الكورس<select aria-label="الكورس" className={input} value={course} disabled={optionsLoading || (audience === 'admin' && !teacher)} onChange={e => { setCourse(e.target.value); setLessons([]); clearResults(); }}><option value="">اختر الكورس</option>{options.packages.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
        <label className="space-y-2 text-sm font-bold text-[var(--admin-text)]">حالة المشاهدة<select aria-label="حالة المشاهدة" className={input} value={state} onChange={e => setState(e.target.value as FollowUpFilter['state'])}><option value="NotStarted">لم يبدأ المشاهدة</option><option value="Started">بدأ المشاهدة ولم يُسجّل اكتماله</option><option value="Incomplete">كل من لم يُسجّل اكتماله</option><option value="Completed">مكتمل</option><option value="All">كل الحالات</option></select></label>
        <label className="space-y-2 text-sm font-bold text-[var(--admin-text)]">مدة التأخير<select aria-label="مدة التأخير" className={input} value={days} onChange={e => setDays(Number(e.target.value))}><option value={0}>بدون حد أدنى</option><option value={7}>أسبوع أو أكثر</option><option value={14}>أسبوعين أو أكثر</option><option value={30}>30 يوم أو أكثر</option></select></label>
        <label className="space-y-2 text-sm font-bold text-[var(--admin-text)]">ابحث عن طالب<input aria-label="ابحث عن طالب" className={input} value={search} onChange={e => setSearch(e.target.value)} placeholder="اسم الطالب أو الموبايل أو الكود" maxLength={100} /></label>
      </div>
      <fieldset className="mt-5 border-t border-[var(--admin-border)] pt-4"><legend className="px-1 text-sm font-bold text-[var(--admin-text)]">الحصص المطلوبة <span className="font-normal">({lessons.length} مختارة، بحد أقصى 20)</span></legend>
        {optionsLoading ? <p role="status" className="py-4 text-sm text-[var(--admin-muted)]">جاري تحميل الاختيارات…</p> : !course ? <p className="py-4 text-sm text-[var(--admin-muted)]">اختر الكورس أولًا لعرض حصصه.</p> : options.lessons.length === 0 ? <p className="py-4 text-sm text-[var(--admin-muted)]">لا توجد حصص بها فيديوهات مفعّلة في هذا الكورس.</p> : <div className="mt-2 grid max-h-64 gap-2 overflow-y-auto sm:grid-cols-2 xl:grid-cols-3">{options.lessons.map(lesson => <label key={lesson.id} className={`flex min-h-12 cursor-pointer items-center gap-3 rounded-xl border p-3 text-sm ${lessons.includes(lesson.id) ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-text)]' : 'border-[var(--admin-border)] text-[var(--admin-text)]'}`}><input type="checkbox" checked={lessons.includes(lesson.id)} disabled={!lessons.includes(lesson.id) && lessons.length >= 20} onChange={e => setLessons(current => e.target.checked ? [...current, lesson.id] : current.filter(id => id !== lesson.id))} className="h-4 w-4 shrink-0 accent-[var(--admin-primary)]" /><span className="min-w-0"><span className="block font-bold">{lesson.title}</span><span className="block text-xs text-[var(--admin-muted)]">{lesson.termTitle}</span></span></label>)}</div>}
      </fieldset>
      <p className="mt-4 text-xs leading-6 text-[var(--admin-muted)]">الأسبوع يبدأ من الأحدث بين فتح الفيديو للطلاب وتاريخ حصول الطالب على صلاحية مشاهدته، وليس من تاريخ إنشاء الحصة. اختيار أكثر من حصة يعرض كل طالب وحصته المطابقة للفلتر.</p>
      {optionsError && <div role="alert" className="mt-3 flex flex-wrap items-center gap-3 text-sm text-[var(--admin-danger)]">{optionsError}<button type="button" onClick={() => setOptionsVersion(value => value + 1)} className="min-h-11 underline">إعادة تحميل الاختيارات</button></div>}
      <div className="mt-5 flex flex-wrap items-center gap-3"><button type="button" onClick={run} disabled={busy || optionsLoading || !!optionsError || !course || lessons.length === 0 || (audience === 'admin' && !teacher)} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 py-2 text-sm font-bold text-white disabled:opacity-50"><Search className="h-4 w-4" />{busy ? 'جاري عرض المتابعة…' : 'عرض الطلاب'}</button><button type="button" onClick={() => void download()} disabled={!report || report.totalRows === 0 || busy || dirty || exporting} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] disabled:opacity-50"><Download className="h-4 w-4" />{exporting ? 'جاري تجهيز الإكسيل…' : 'تنزيل النتيجة إكسيل'}</button>{dirty && <p role="status" className="text-sm text-[var(--admin-muted)]">الفلاتر اتغيّرت. اضغط «عرض الطلاب» لتحديث النتيجة والتصدير.</p>}</div>
    </section>
    <section ref={resultsRef} tabIndex={-1} aria-label="نتيجة متابعة الطلاب" className="space-y-4 outline-none">
      {error && <div role="alert" className="rounded-xl border border-[var(--admin-danger)] p-4 text-sm text-[var(--admin-danger)]">{error}{applied && <button type="button" onClick={run} className="mr-3 inline-flex min-h-11 items-center gap-1 underline"><RefreshCw className="h-4 w-4" />إعادة عرض الطلاب</button>}</div>}
      <FollowUpResults report={report} busy={busy} dirty={dirty} onPage={page => { if (applied) void load({ ...applied, page }); }} />
    </section>
  </div>;
}
function message(reason: unknown, fallback: string): string {
  const error = reason as { response?: { data?: { message?: string } }; message?: string };
  return error.response?.data?.message || (reason instanceof Error && !error.response ? reason.message : fallback);
}
async function exportError(reason: unknown): Promise<string> {
  const data = (reason as { response?: { data?: unknown } }).response?.data;
  if (data instanceof Blob) { try { return JSON.parse(await data.text()).message || 'تعذر تنزيل الإكسيل.'; } catch { return 'تعذر تنزيل الإكسيل.'; } }
  return message(reason, 'تعذر تنزيل الإكسيل. أعد المحاولة.');
}

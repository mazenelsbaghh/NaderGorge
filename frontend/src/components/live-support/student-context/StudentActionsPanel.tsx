'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, LoaderCircle, Play, X } from 'lucide-react';
import {
  liveSupportService,
  type LiveSupportActionDefinition,
} from '@/services/live-support-service';
import { studentActionFields } from './student-action-definitions';
import { createClientId } from '@/lib/client-id';

type FieldValue = string | number | boolean;
type VideoOption = { id: string; lessonId: string; teacher: string; subject: string; packageName: string; term: string; course: string; lesson: string; title: string; watchCount: number; maxWatchCount: number };
type VideoSelection = { teacher: string; subject: string; packageName: string; term: string; course: string; videoId: string };
type LabeledOption = { id: string; label: string };
type StudentActionContext = { balance: number; points: number; videos: VideoOption[]; devices: LabeledOption[]; notes: LabeledOption[]; grants: LabeledOption[]; watchRequests: LabeledOption[]; staff: LabeledOption[] };

const emptyVideoSelection: VideoSelection = { teacher: '', subject: '', packageName: '', term: '', course: '', videoId: '' };
const unique = (items: string[]) => [...new Set(items.filter(Boolean))];

export function StudentActionsPanel({
  conversationId,
  hasStudent,
  onCompleted,
}: {
  conversationId: string;
  hasStudent: boolean;
  onCompleted: () => void;
}) {
  const [catalog, setCatalog] = useState<LiveSupportActionDefinition[]>([]);
  const [catalogLoading, setCatalogLoading] = useState(true);
  const [catalogError, setCatalogError] = useState('');
  const [selected, setSelected] = useState<LiveSupportActionDefinition>();
  const [values, setValues] = useState<Record<string, FieldValue>>({});
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState('');
  const [draftLoading, setDraftLoading] = useState(false);
  const [studentContext, setStudentContext] = useState<StudentActionContext>();
  const [studentContextError, setStudentContextError] = useState('');
  const [videoSelection, setVideoSelection] = useState<VideoSelection>(emptyVideoSelection);
  
  const available = useMemo(
    () => catalog.filter((item) => hasStudent ? item.key !== 'student.create-and-link' : true),
    [catalog, hasStudent]
  );

  const loadCatalog = useCallback(async () => {
    setCatalogLoading(true);
    setCatalogError('');
    try {
      setCatalog(await liveSupportService.getActionCatalog(conversationId));
    } catch {
      setCatalogError('تعذر تحميل قائمة الإجراءات. أعد المحاولة.');
    } finally {
      setCatalogLoading(false);
    }
  }, [conversationId]);

  const loadStudentContext = useCallback(async () => {
    if (!hasStudent) return;
    try {
      setStudentContext(await liveSupportService.getStudentActionContext(conversationId));
      setStudentContextError('');
    } catch {
      setStudentContextError('تعذر تحميل بيانات إجراءات الطالب. أعد المحاولة قبل تنفيذ الإجراء.');
    }
  }, [conversationId, hasStudent]);

  useEffect(() => {
    void loadCatalog();
    void loadStudentContext();
  }, [loadCatalog, loadStudentContext]);

  const videoOptions = useMemo(() => studentContext?.videos ?? [], [studentContext]);
  const filteredVideoOptions = useMemo(() => videoOptions.filter((video) =>
    (!videoSelection.teacher || video.teacher === videoSelection.teacher) &&
    (!videoSelection.subject || video.subject === videoSelection.subject) &&
    (!videoSelection.packageName || video.packageName === videoSelection.packageName) &&
    (!videoSelection.term || video.term === videoSelection.term) &&
    (!videoSelection.course || video.course === videoSelection.course)
  ), [videoOptions, videoSelection]);
  const idFieldOptions = useMemo<Record<string, LabeledOption[]>>(() => ({
    noteId: studentContext?.notes ?? [],
    deviceId: studentContext?.devices ?? [],
    accessGrantId: studentContext?.grants ?? [],
    requestId: studentContext?.watchRequests ?? [],
    lessonId: unique(videoOptions.map(video => video.lessonId)).map(id => {
      const video = videoOptions.find(item => item.lessonId === id)!;
      return { id, label: `${video.teacher} · ${video.subject} · ${video.lesson}` };
    }),
    assignedAgentId: studentContext?.staff ?? [],
  }), [studentContext, videoOptions]);

  function updateVideoSelection(change: Partial<VideoSelection>, fieldKey: string) {
    const next = { ...videoSelection, ...change };
    if ('teacher' in change) Object.assign(next, { subject: '', packageName: '', term: '', course: '', videoId: '' });
    if ('subject' in change) Object.assign(next, { packageName: '', term: '', course: '', videoId: '' });
    if ('packageName' in change) Object.assign(next, { term: '', course: '', videoId: '' });
    if ('term' in change) Object.assign(next, { course: '', videoId: '' });
    if ('course' in change) next.videoId = '';
    setVideoSelection(next);
    setValues(current => ({ ...current, [fieldKey]: next.videoId }));
  }

  async function choose(action: LiveSupportActionDefinition) {
    setSelected(action);
    setConfirming(false);
    setResult('');
    setFieldErrors({});
    setVideoSelection(emptyVideoSelection);
    setDraftLoading(true);
    if (hasStudent && !studentContext) void loadStudentContext();
    try {
      const draft = await liveSupportService.getActionDraft(conversationId, action.key);
      setValues(Object.fromEntries((studentActionFields[action.key] ?? []).map((field) => [
        field.key,
        draft[field.key] === null || draft[field.key] === undefined
          ? field.type === 'checkbox' ? false : ''
          : draft[field.key],
      ])) as Record<string, FieldValue>);
    } catch {
      setValues(Object.fromEntries((studentActionFields[action.key] ?? []).map((field) => [field.key, field.type === 'checkbox' ? false : ''])));
      setResult('تعذر تحميل البيانات الحالية؛ راجع القيم قبل التنفيذ.');
    } finally {
      setDraftLoading(false);
    }
  }

  function validateForm(): boolean {
    if (!selected) return false;
    const errors: Record<string, string> = {};
    const fields = studentActionFields[selected.key] ?? [];
    
    for (const field of fields) {
      const val = values[field.key];
      if (field.required && (val === undefined || val === null || val === '')) {
        errors[field.key] = 'هذا الحقل مطلوب';
      }
      if (field.type === 'number' && val !== '' && isNaN(Number(val))) {
        errors[field.key] = 'يجب إدخال رقم صحيح';
      }
    }
    
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  }

  async function execute() {
    if (!selected) return;
    setBusy(true);
    setResult('');
    try {
      const payload = Object.fromEntries(
        Object.entries(values)
          .filter(([, value]) => value !== '')
          .map(([key, value]) => [
            key,
            studentActionFields[selected.key]?.find(
              (field) => field.key === key
            )?.type === 'number'
              ? Number(value)
              : value,
          ])
      );
      const response = await liveSupportService.executeStudentAction<
        Record<string, unknown>,
        { message: string }
      >(
        conversationId,
        selected.key,
        createClientId(),
        selected.confirmationVersion,
        payload
      );
      setResult(response.message);
      setConfirming(false);
      onCompleted();
    } catch (cause) {
      setResult(
        (cause as { response?: { data?: { message?: string } } }).response?.data
          ?.message ?? 'تعذر تنفيذ الإجراء.'
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-3">
      <h3 className="font-bold text-slate-900">إجراءات الطالب</h3>
      <p className="mt-1 text-xs text-slate-500">
        كل إجراء يحتاج تأكيدًا ويُسجل باسمك ووقت تنفيذه.
      </p>
      {!hasStudent && (
        <p className="mt-3 rounded-xl border border-amber-200 bg-amber-50 p-3 text-xs leading-5 text-amber-900">
          اربط المحادثة بطالب أولًا لتفعيل إجراءات حسابه. ستظل كل الإجراءات ظاهرة هنا لتعرف ما هو متاح.
        </p>
      )}
      {hasStudent && studentContext && <div className="mt-3 grid grid-cols-2 gap-2 rounded-xl bg-slate-50 p-2 text-sm"><p className="text-slate-600">الرصيد <strong className="mr-1 text-slate-900">{studentContext.balance} ج.م</strong></p><p className="text-slate-600">النقاط <strong className="mr-1 text-slate-900">{studentContext.points}</strong></p></div>}
      {catalogError ? (
        <div className="mt-3 rounded-xl border border-red-200 bg-red-50 p-3 text-xs text-red-800">
          <p>{catalogError}</p>
          <button type="button" onClick={() => void loadCatalog()} className="mt-2 font-bold underline">إعادة المحاولة</button>
        </div>
      ) : catalogLoading ? (
        <div className="mt-3 flex items-center gap-2 rounded-xl bg-slate-50 p-3 text-xs text-slate-600"><LoaderCircle size={15} className="animate-spin" /> جارٍ تحميل الإجراءات…</div>
      ) : <div className="mt-3 grid grid-cols-2 gap-2">
        {available.map((action) => (
          <button
            key={action.key}
            type="button"
            disabled={!hasStudent && action.key !== 'student.create-and-link'}
            onClick={() => choose(action)}
            title={!hasStudent && action.key !== 'student.create-and-link' ? 'اربط المحادثة بطالب لتفعيل هذا الإجراء' : undefined}
            className={`rounded-xl border p-2 text-right text-xs font-semibold disabled:cursor-not-allowed disabled:border-slate-200 disabled:bg-slate-100 disabled:text-slate-400 ${action.danger === 'financial' ? 'border-amber-300 bg-amber-50 text-amber-900' : action.danger === 'high' ? 'border-red-200 bg-red-50 text-red-800' : 'border-slate-200 text-slate-700 hover:border-cyan-600'}`}
          >
            {action.labelAr}
          </button>
        ))}
      </div>}
      {selected && (
        <div
          className="fixed inset-0 z-[120] grid place-items-center bg-slate-950/60 p-4"
          onClick={() => !busy && setSelected(undefined)}
        >
          <div
            role="dialog"
            aria-modal="true"
            onClick={(event) => event.stopPropagation()}
            className="flex max-h-[calc(100dvh-2rem)] w-full max-w-6xl flex-col overflow-hidden rounded-2xl bg-white"
            dir="rtl"
          >
            <div className="shrink-0 border-b border-slate-200 px-5 py-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-bold">{selected.labelAr}</h3>
              </div>
              <button
                disabled={busy}
                onClick={() => setSelected(undefined)}
                aria-label="إغلاق"
                className="grid size-10 place-items-center rounded-full hover:bg-slate-100 disabled:opacity-50"
              >
                <X size={18} />
              </button>
            </div>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 pb-5">
            {draftLoading ? (
              <div className="mt-8 grid place-items-center gap-3 py-8 text-sm text-slate-600"><LoaderCircle className="animate-spin" /> جارٍ تحميل البيانات الحالية…</div>
            ) : !confirming ? (
              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  if (validateForm()) {
                    setConfirming(true);
                  }
                }}
                className="mt-5 grid grid-cols-1 gap-3 md:grid-cols-2"
              >
                {studentContextError && (
                  <div className="flex items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                    <span>{studentContextError}</span>
                    <button type="button" onClick={() => void loadStudentContext()} className="shrink-0 rounded-lg border border-amber-300 px-3 py-1.5 font-semibold hover:bg-amber-100">
                      إعادة المحاولة
                    </button>
                  </div>
                )}
                {(studentActionFields[selected.key] ?? []).map((field) => (
                  <div key={field.key} className={`min-w-0 space-y-1 ${field.key === 'address' || field.key === 'videoId' || field.key === 'lessonVideoId' ? 'md:col-span-2' : ''}`}>
                    <label
                      className={
                        field.type === 'checkbox'
                          ? 'flex items-center gap-2 text-sm cursor-pointer'
                          : 'block text-sm font-semibold text-slate-700'
                      }
                    >
                      {field.type === 'checkbox' ? (
                        <>
                          <input
                            type="checkbox"
                            disabled={busy}
                            checked={Boolean(values[field.key])}
                            onChange={(event) =>
                              setValues({
                                ...values,
                                [field.key]: event.target.checked,
                              })
                            }
                            className="size-5 rounded border-slate-300 text-cyan-600 focus:ring-cyan-500"
                          />
                          {field.label}
                        </>
                      ) : (
                        <>
                          {field.label}
                          {field.key === 'videoId' || field.key === 'lessonVideoId' ? (
                            <div className="mt-1 grid gap-2 md:grid-cols-6">
                              <select disabled={busy || !videoOptions.length} value={videoSelection.teacher} onChange={(event) => updateVideoSelection({ teacher: event.target.value }, field.key)} className="h-11 min-w-0 rounded-xl border border-slate-200 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"><option value="">اختر المدرس</option>{unique(videoOptions.map(video => video.teacher)).map(value => <option key={value} value={value}>{value}</option>)}</select>
                              <select disabled={busy || !videoSelection.teacher} value={videoSelection.subject} onChange={(event) => updateVideoSelection({ subject: event.target.value }, field.key)} className="h-11 min-w-0 rounded-xl border border-slate-200 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"><option value="">اختر المادة</option>{unique(filteredVideoOptions.map(video => video.subject)).map(value => <option key={value} value={value}>{value}</option>)}</select>
                              <select disabled={busy || !videoSelection.subject} value={videoSelection.packageName} onChange={(event) => updateVideoSelection({ packageName: event.target.value }, field.key)} className="h-11 min-w-0 rounded-xl border border-slate-200 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"><option value="">اختر الباقة</option>{unique(filteredVideoOptions.map(video => video.packageName)).map(value => <option key={value} value={value}>{value}</option>)}</select>
                              <select disabled={busy || !videoSelection.packageName} value={videoSelection.term} onChange={(event) => updateVideoSelection({ term: event.target.value }, field.key)} className="h-11 min-w-0 rounded-xl border border-slate-200 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"><option value="">اختر الترم</option>{unique(filteredVideoOptions.map(video => video.term)).map(value => <option key={value} value={value}>{value}</option>)}</select>
                              <select disabled={busy || !videoSelection.term} value={videoSelection.course} onChange={(event) => updateVideoSelection({ course: event.target.value }, field.key)} className="h-11 min-w-0 rounded-xl border border-slate-200 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"><option value="">اختر الكورس</option>{unique(filteredVideoOptions.map(video => video.course)).map(value => <option key={value} value={value}>{value}</option>)}</select>
                              <select disabled={busy || !videoSelection.course} required={field.required} value={videoSelection.videoId} onChange={(event) => updateVideoSelection({ videoId: event.target.value }, field.key)} className={`h-11 min-w-0 rounded-xl border px-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 ${fieldErrors[field.key] ? 'border-red-500' : 'border-slate-200'}`}><option value="">اختر الفيديو</option>{filteredVideoOptions.map(video => <option key={video.id} value={video.id}>{`${video.lesson} · ${video.title} (${video.watchCount}/${video.maxWatchCount})`}</option>)}</select>
                            </div>
                          ) : idFieldOptions[field.key] ? (
                            <select disabled={busy || !idFieldOptions[field.key].length} required={field.required} value={String(values[field.key] ?? '')} onChange={(event) => setValues({ ...values, [field.key]: event.target.value })} className={`mt-1 h-11 w-full rounded-xl border px-3 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 ${fieldErrors[field.key] ? 'border-red-500' : 'border-slate-200'}`}>
                              <option value="">{idFieldOptions[field.key].length ? `اختر ${field.label.replace('معرّف ', '')}` : 'لا توجد اختيارات متاحة'}</option>
                              {idFieldOptions[field.key].map(option => <option key={option.id} value={option.id}>{option.label}</option>)}
                            </select>
                          ) : field.type === 'select' ? (
                            <select
                              disabled={busy}
                              required={field.required}
                              value={String(values[field.key] ?? '')}
                              onChange={(event) =>
                                setValues({
                                  ...values,
                                  [field.key]: event.target.value,
                                })
                              }
                              className={`mt-1 h-11 w-full rounded-xl border px-3 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 ${fieldErrors[field.key] ? 'border-red-500' : 'border-slate-200'}`}
                            >
                              <option value="">اختر</option>
                              {field.options?.map((option) => (
                                <option key={option.value} value={option.value}>{option.label}</option>
                              ))}
                            </select>
                          ) : (
                            <input
                              type={
                                field.type === 'datetime'
                                  ? 'datetime-local'
                                  : field.type
                              }
                              disabled={busy}
                              required={field.required}
                              value={String(values[field.key] ?? '')}
                              onChange={(event) =>
                                setValues({
                                  ...values,
                                  [field.key]: event.target.value,
                                })
                              }
                              className={`mt-1 h-11 w-full rounded-xl border px-3 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 ${fieldErrors[field.key] ? 'border-red-500' : 'border-slate-200'}`}
                            />
                          )}
                        </>
                      )}
                    </label>
                    {fieldErrors[field.key] && (
                      <p className="text-xs font-semibold text-red-600">{fieldErrors[field.key]}</p>
                    )}
                  </div>
                ))}
                <div className="sticky bottom-0 z-10 -mx-5 mt-2 border-t border-slate-200 bg-white px-5 pt-3 md:col-span-2">
                <button
                  type="submit"
                  disabled={busy}
                  className="h-11 w-full rounded-xl bg-slate-900 font-semibold text-white hover:bg-slate-800 disabled:bg-slate-300 transition-colors"
                >
                  مراجعة وتأكيد
                </button>
                </div>
              </form>
            ) : (
              <div className="mt-5">
                <div className={`rounded-2xl border p-4 ${selected.danger === 'financial' ? 'border-amber-200 bg-amber-50 text-amber-900' : selected.danger === 'high' ? 'border-red-200 bg-red-50 text-red-800' : 'border-slate-200 bg-slate-50 text-slate-800'}`}>
                  <AlertTriangle className={`mb-2 ${selected.danger === 'financial' ? 'text-amber-700' : selected.danger === 'high' ? 'text-red-700' : 'text-slate-700'}`} />
                  <h4 className="font-bold">تأكيد التنفيذ: {selected.labelAr}</h4>
                  <p className="mt-2 text-xs">
                    التصنيف: {selected.category}
                  </p>
                  <p className="mt-1 text-xs leading-5">
                    سيتم تنفيذ هذا الإجراء على الطالب المرتبط وتسجيل العملية كاملة. هذا الإجراء ذو خطورة: {selected.danger === 'financial' ? 'مالية' : selected.danger === 'high' ? 'عالية' : 'عادية'}.
                  </p>
                </div>
                <div className="mt-4 flex gap-2">
                  <button
                    disabled={busy}
                    onClick={() => setConfirming(false)}
                    className="h-11 flex-1 rounded-xl border hover:bg-slate-50 disabled:opacity-50"
                  >
                    رجوع
                  </button>
                  <button
                    disabled={busy}
                    onClick={() => void execute()}
                    className="inline-flex h-11 flex-1 items-center justify-center gap-2 rounded-xl bg-red-700 font-semibold text-white hover:bg-red-800 disabled:bg-slate-300 transition-colors"
                  >
                    {busy ? (
                      <LoaderCircle className="animate-spin" size={17} />
                    ) : (
                      <Play size={17} />
                    )}
                    {selected.danger === 'financial'
                      ? `تأكيد ${selected.labelAr}`
                      : 'تنفيذ الإجراء'}
                  </button>
                </div>
              </div>
            )}
            {result && (
              <p
                role="status"
                className="mt-3 rounded-xl bg-slate-100 p-3 text-sm font-semibold"
              >
                {result}
              </p>
            )}
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

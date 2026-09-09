'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { AdminPage } from './AdminShellChrome';
import { AdminBackButton } from './AdminBackButton';
import { TeacherShellChrome } from '@/components/teacher/TeacherShellChrome';
import { QuestionEditor } from './QuestionEditor';
import { OcrQuestionImport } from './OcrQuestionImport';
import { teacherService, type SubjectDto } from '@/services/teacher-service';
import { fromQuestionEditor, newRevisionQuestion, toQuestionEditor } from './assessment-revision-mapping';
import { assessmentRevisionService, defaultRevisionPolicy, type AssessmentDefinition, type AssessmentKind,
  type RevisionPolicy, type RevisionPreview } from '@/services/assessment-revision-service';
import { getApiErrorSummary } from '@/lib/api-errors';
import { createClientId } from '@/lib/client-id';
import { questionTextToPlainText } from '@/lib/question-text';

const button = 'min-h-11 rounded-xl border border-[var(--admin-border)] px-4 py-2 text-sm font-bold hover:bg-[var(--admin-card-soft)] disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--admin-primary)]';
const primary = `${button} bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:brightness-110`;

export function AssessmentDefinitionEditor({ id, kind, surface = 'admin', initialQuestionId }: {
  id: string; kind: AssessmentKind; surface?: 'admin' | 'teacher'; initialQuestionId?: string;
}) {
  const router = useRouter();
  const [definition, setDefinition] = useState<AssessmentDefinition | null>(null);
  const [attemptCount, setAttemptCount] = useState(0);
  const [policy, setPolicy] = useState<RevisionPolicy>(defaultRevisionPolicy);
  const [selectedId, setSelectedId] = useState<string | null>(initialQuestionId ?? null);
  const [preview, setPreview] = useState<RevisionPreview | null>(null);
  const [operationId, setOperationId] = useState<string | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [subjectId, setSubjectId] = useState('');
  const label = kind === 'exam' ? 'الامتحان' : 'الواجب';
  const profilePath = `${surface === 'teacher' ? '/teacher/packages' : '/admin/content'}/${kind === 'exam' ? 'exams' : 'homework'}/${id}`;

  useEffect(() => {
    let cancelled = false;
    assessmentRevisionService.load(kind, id).then(editor => {
      if (cancelled) return;
      setDefinition(editor.definition);
      setAttemptCount(editor.attemptCount);
      if (kind === 'exam' && editor.definition.questions.length === 0) {
        void teacherService.getSubjects().then(response => { if (!cancelled) setSubjects(response.data); })
          .catch(cause => { if (!cancelled) setError(getApiErrorSummary(cause, 'تعذر تحميل المواد.')); });
      }
    }).catch(cause => { if (!cancelled) setError(getApiErrorSummary(cause, 'تعذر تحميل التقييم.')); });
    return () => { cancelled = true; };
  }, [id, kind]);

  function invalidatePreview() { setPreview(null); setOperationId(null); setConfirmed(false); setError(''); }
  function updateDefinition(changes: Partial<AssessmentDefinition>) {
    setDefinition(current => current ? { ...current, ...changes } : null);
    invalidatePreview();
  }
  function updatePolicy(changes: Partial<RevisionPolicy>) { setPolicy(current => ({ ...current, ...changes })); invalidatePreview(); }
  function moveQuestion(index: number, offset: number) {
    if (!definition) return;
    const questions = [...definition.questions];
    [questions[index], questions[index + offset]] = [questions[index + offset], questions[index]];
    updateDefinition({ questions: questions.map((question, position) => ({ ...question, order: position + 1 })) });
  }
  async function previewChanges() {
    if (!definition) return;
    setBusy(true); setError('');
    try {
      setPreview(await assessmentRevisionService.preview(definition, policy));
      setOperationId(createClientId()); setConfirmed(false);
    } catch (cause) { setError(getApiErrorSummary(cause, 'تعذر حساب تأثير التعديل.')); }
    finally { setBusy(false); }
  }
  async function saveChanges() {
    if (!definition || !preview || !operationId) return;
    setBusy(true); setError('');
    try {
      await assessmentRevisionService.save({ definition, policy, revisionToken: preview.revisionToken,
        operationId, confirmPreviousAttempts: confirmed, subjectId: subjectId || undefined });
      router.push(profilePath); router.refresh();
    } catch (cause) { setError(getApiErrorSummary(cause, 'تعذر الحفظ. التعديلات ما زالت موجودة هنا.')); }
    finally { setBusy(false); }
  }

  const selectedIndex = definition?.questions.findIndex(question => question.id === selectedId) ?? -1;
  const content = (
    <div className="mx-auto w-full max-w-5xl space-y-6 text-[var(--admin-text)]" dir="rtl">
      {error && <p role="alert" className="rounded-xl border border-[var(--admin-danger)] p-4 text-sm leading-7 text-[var(--admin-danger)]">{error}</p>}
      {!definition ? <p role="status">{error ? 'ارجع لبروفايل التقييم وحاول فتح المحرر مرة أخرى.' : 'جاري تحميل إعدادات التقييم والأسئلة…'}</p> : <>
        <p className="text-sm leading-7 text-[var(--admin-muted)]">تقدر تعدّل التقييم بالكامل. فيه {attemptCount} محاولة مسجلة. اختَر تأثير التعديل على المحاولات السابقة قبل الحفظ.</p>
        <fieldset disabled={busy} className="space-y-5 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
          <legend className="px-2 text-lg font-bold">إعدادات {label}</legend>
          <label className="block space-y-2"><span>اسم {label}</span><input className="admin-input w-full" maxLength={255} value={definition.title} onChange={event => updateDefinition({ title: event.target.value })} /></label>
          <label className="block space-y-2"><span>الوصف والتعليمات</span><textarea className="admin-input min-h-24 w-full" value={definition.description ?? ''} onChange={event => updateDefinition({ description: event.target.value })} /></label>
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="space-y-2"><span>الدرجة النهائية</span><input type="number" min="0" step="0.01" className="admin-input w-full" value={definition.totalScore} onChange={event => updateDefinition({ totalScore: Number(event.target.value) })} /></label>
            <label className="space-y-2"><span>درجة النجاح</span><input type="number" min="0" step="0.01" className="admin-input w-full" value={definition.passingScore ?? 0} onChange={event => updateDefinition({ passingScore: Number(event.target.value) })} /></label>
            <label className="space-y-2"><span>المدة بالدقائق، اتركها فارغة بدون مؤقت</span><input type="number" min="1" className="admin-input w-full" value={definition.durationMinutes ?? ''} onChange={event => updateDefinition({ durationMinutes: event.target.value ? Number(event.target.value) : null })} /></label>
            {kind === 'exam' && <>
              <label className="space-y-2"><span>عدد الأسئلة المعروضة، فارغ لعرض الكل</span><input type="number" min="1" max={definition.questions.length} className="admin-input w-full" value={definition.displayQuestionCount ?? ''} onChange={event => updateDefinition({ displayQuestionCount: event.target.value ? Number(event.target.value) : null })} /></label>
            </>}
          </div>
          <div className="flex flex-wrap gap-6">
            <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={definition.isActive} onChange={event => updateDefinition({ isActive: event.target.checked })} />متاح للطلاب</label>
            <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={definition.isMandatory} onChange={event => updateDefinition({ isMandatory: event.target.checked })} />{label} إلزامي</label>
            <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={definition.isRandomized} onChange={event => updateDefinition({ isRandomized: event.target.checked })} />ترتيب عشوائي للأسئلة</label>
          </div>
        </fieldset>
        <section aria-label="تعديل الأسئلة" className="space-y-4">
          {subjects.length > 0 && <label className="block space-y-2"><span>مادة أسئلة الامتحان</span><select className="admin-input w-full" disabled={busy} value={subjectId} onChange={event => { setSubjectId(event.target.value); invalidatePreview(); }}><option value="">اختر المادة</option>{subjects.map(subject => <option key={subject.id} value={subject.id}>{subject.name}</option>)}</select></label>}
          <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-lg font-bold">الأسئلة ({definition.questions.length})</h2>
            <button type="button" className={button} disabled={busy} onClick={() => { const question = newRevisionQuestion(definition.questions.length + 1); updateDefinition({ questions: [...definition.questions, question] }); setSelectedId(question.id); }}>إضافة سؤال</button>
          </div>
          <fieldset disabled={busy}><OcrQuestionImport nextOrder={definition.questions.length + 1} onImport={imported => {
            const added = imported.map((question, index) => fromQuestionEditor(newRevisionQuestion(definition.questions.length + index + 1), question));
            updateDefinition({ questions: [...definition.questions, ...added] });
            setSelectedId(added[0]?.id ?? null);
          }} /></fieldset>
          <ol className="divide-y divide-[var(--admin-border)]">
            {definition.questions.map((question, index) => <li key={question.id} className="flex flex-wrap items-center gap-2 py-3">
              <button type="button" disabled={busy} className={`${button} min-w-0 flex-1 text-start`} onClick={() => setSelectedId(question.id)} aria-expanded={selectedId === question.id}>
                {index + 1}. {questionTextToPlainText(question.text).slice(0, 100) || 'سؤال جديد'}
              </button>
              <button type="button" className={button} disabled={busy || index === 0} aria-label={`نقل السؤال ${index + 1} لأعلى`} onClick={() => moveQuestion(index, -1)}>↑</button>
              <button type="button" className={button} disabled={busy || index === definition.questions.length - 1} aria-label={`نقل السؤال ${index + 1} لأسفل`} onClick={() => moveQuestion(index, 1)}>↓</button>
            </li>)}
          </ol>
          {definition.questions.length === 0 && <p className="text-sm text-[var(--admin-muted)]">لا توجد أسئلة. إضافة سؤال تبدأ من الزر بالأعلى. حفظ التقييم بدون أسئلة يعطّله للطلاب.</p>}
          {selectedIndex >= 0 && <fieldset disabled={busy} className="min-w-0" id="assessment-question-editor">
            <QuestionEditor key={selectedId} index={selectedIndex} question={toQuestionEditor(definition.questions[selectedIndex])}
              onChange={(index, edited) => { const next = fromQuestionEditor(definition.questions[index], edited); updateDefinition({ questions: definition.questions.map((question, position) => position === index ? next : question) }); setSelectedId(next.id); }}
              onRemove={index => { updateDefinition({ questions: definition.questions.filter((_, position) => position !== index).map((question, position) => ({ ...question, order: position + 1 })) }); setSelectedId(null); }} />
          </fieldset>}
        </section>
        <fieldset disabled={busy} className="space-y-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
          <legend className="px-2 text-lg font-bold">تأثير التعديل على المحاولات السابقة</legend>
          <label className="block space-y-2"><span>تطبيق التعديل</span><select className="admin-input w-full" value={policy.previousAttempts} onChange={event => updatePolicy({ previousAttempts: event.target.value as RevisionPolicy['previousAttempts'] })}>
            <option value="Preserve">المحاولات الجديدة فقط، الحفاظ على النتائج السابقة</option><option value="Regrade">إعادة تصحيح المحاولات السابقة</option>
          </select></label>
          {policy.previousAttempts === 'Regrade' && <>
            <label className="block space-y-2"><span>عند حذف سؤال أو تغيير نوعه</span><select className="admin-input w-full" value={policy.removedQuestions} onChange={event => updatePolicy({ removedQuestions: event.target.value as RevisionPolicy['removedQuestions'] })}>
              <option value="KeepPreviousGrade">احتفظ بدرجة السؤال القديمة</option><option value="Exclude">استبعد السؤال من حساب الدرجة</option><option value="AwardFullPoints">امنح درجته كاملة</option>
            </select></label>
            <label className="block space-y-2"><span>الأسئلة المضافة</span><select className="admin-input w-full" value={policy.addedQuestions} onChange={event => updatePolicy({ addedQuestions: event.target.value as RevisionPolicy['addedQuestions'] })}>
              <option value="FutureAttemptsOnly">تظهر في المحاولات الجديدة فقط</option><option value="RequestCompletion">اطلب من الطلاب السابقين استكمالها</option>
            </select></label>
            <label className="block space-y-2"><span>التصحيح اليدوي السابق</span><select className="admin-input w-full" value={policy.manualGrades} onChange={event => updatePolicy({ manualGrades: event.target.value as RevisionPolicy['manualGrades'] })}>
              <option value="Preserve">احتفظ بالدرجات اليدوية ومقياسها الأصلي</option><option value="ReturnForReview">أعدها للمراجعة والتصحيح</option>
            </select></label>
            <label className="block space-y-2"><span>انخفاض درجات الطلاب</span><select className="admin-input w-full" value={policy.scoreDecrease} onChange={event => updatePolicy({ scoreDecrease: event.target.value as RevisionPolicy['scoreDecrease'] })}>
              <option value="Prevent">لا تقل النسبة عن النتيجة السابقة</option><option value="Allow">اسمح بزيادة الدرجة أو نقصانها حسب التصحيح</option>
            </select></label>
            <p className="text-sm leading-7 text-[var(--admin-muted)]">منع النقص يحافظ على نسبة النتيجة السابقة، حتى عند تغيير الدرجة النهائية أو استكمال الأسئلة المضافة. المعاينة توضح الأثر قبل تطبيقه. تغيير نوع السؤال يُحسب حذفًا للسؤال القديم وإضافة سؤال جديد.</p>
          </>}
        </fieldset>
        <button type="button" className={button} disabled={busy} onClick={() => void previewChanges()}>{busy ? 'جاري التحقق…' : 'معاينة تأثير التعديل'}</button>
        {preview && <section aria-label="معاينة تأثير التعديل" className="space-y-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
          <h2 className="text-lg font-bold">مراجعة قبل الحفظ</h2>
          <p className="text-sm">{preview.addedQuestions} سؤال مضاف، {preview.removedQuestions} سؤال محذوف، {preview.attemptCount} محاولة سابقة.</p>
          {policy.previousAttempts === 'Preserve' ? <p>الإجابات والدرجات السابقة لن تتغير.</p> : <div className="max-h-80 overflow-auto">
            <table className="w-full text-start text-sm"><thead><tr><th className="p-2 text-start">المحاولة</th><th className="p-2">قبل</th><th className="p-2">بعد</th><th className="p-2">الحالة</th></tr></thead><tbody>
              {preview.attempts.map((attempt, index) => <tr key={attempt.attemptId} className="border-t border-[var(--admin-border)]">
                <td className="p-2" title={attempt.attemptId}>محاولة {index + 1}</td><td className="p-2 text-center">{attempt.previousScore}</td><td className="p-2 text-center">{attempt.revisedScore ?? 'غير نهائية'}</td>
                <td className="p-2">{attempt.requiresCompletion ? 'مطلوب استكمال' : attempt.requiresReview ? 'تحتاج تصحيحًا' : 'محسوبة'}</td>
              </tr>)}
            </tbody></table>
          </div>}
          {policy.previousAttempts === 'Regrade' && <label className="flex min-h-11 items-start gap-3 text-sm leading-7"><input type="checkbox" className="mt-2" checked={confirmed} disabled={busy} onChange={event => setConfirmed(event.target.checked)} />راجعت الأثر وأوافق على تطبيق اختياراتي على المحاولات السابقة.</label>}
          <button type="button" className={primary} disabled={busy || (policy.previousAttempts === 'Regrade' && !confirmed)} onClick={() => void saveChanges()}>{busy ? 'جاري الحفظ…' : 'حفظ التعديلات المؤكدة'}</button>
        </section>}
      </>}
    </div>
  );
  return surface === 'teacher'
    ? <TeacherShellChrome activePath="/teacher/packages" sectionLabel="المحتوى الدراسي" pageTitle={`تعديل ${label}`} subtitle="الإعدادات والأسئلة وتأثير التعديل" action={<AdminBackButton />}>{content}</TeacherShellChrome>
    : <AdminPage activePath="/admin/content" sectionLabel="إدارة المحتوى" pageTitle={`تعديل ${label}`} subtitle="الإعدادات والأسئلة وتأثير التعديل" action={<AdminBackButton />}>{content}</AdminPage>;
}

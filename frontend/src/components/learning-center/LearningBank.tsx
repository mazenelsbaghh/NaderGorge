'use client';

import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import {
  learningCenterService,
  type LearningOptions,
  type LearningQuestion,
  type PagedQuestions,
  type QuestionInput,
} from '@/services/learning-center-service';
import { isRequestCancellation } from '@/services/api-client';
import { questionTextToPlainText } from '@/lib/question-text';
import { LearningQuestionEditor } from './LearningQuestionEditor';

export function LearningBank({
  options,
  onChanged,
}: {
  options: LearningOptions;
  onChanged: () => void;
}) {
  const [questions, setQuestions] = useState<PagedQuestions | null>(null);
  const [filter, setFilter] = useState({
    search: '',
    teacherId: '',
    subjectId: '',
    packageId: '',
    lessonId: '',
    difficulty: '',
    page: 1,
  });
  const [editor, setEditor] = useState<{
    question?: LearningQuestion;
    classify?: boolean;
  } | null>(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const [importing, setImporting] = useState(false);
  const [importLessonId, setImportLessonId] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setQuestions(null);
      setError('');
      learningCenterService
        .questions(
          {
            ...filter,
            teacherId: filter.teacherId || undefined,
            subjectId: filter.subjectId || undefined,
            packageId: filter.packageId || undefined,
            lessonId: filter.lessonId || undefined,
            difficulty: filter.difficulty || undefined,
          },
          controller.signal
        )
        .then(setQuestions)
        .catch((failure: unknown) => {
          if (!isRequestCancellation(failure))
            setError('تعذر تحميل بنك الأسئلة. أعد المحاولة.');
        });
    }, 200);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [filter, revision]);
  const refresh = () => {
    setEditor(null);
    setRevision(revision + 1);
    onChanged();
  };
  function template() {
    const sample: Omit<QuestionInput, 'lessonId'> = {
      text: 'نص السؤال',
      concept: 'اسم الفكرة',
      difficulty: 2,
      points: 1,
      tags: '',
      correction: 'شرح الإجابة',
      options: [
        { text: 'الإجابة الصحيحة', isCorrect: true },
        { text: 'إجابة أخرى', isCorrect: false },
      ],
    };
    const url = URL.createObjectURL(
      new Blob([JSON.stringify([sample], null, 2)], {
        type: 'application/json',
      })
    );
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'question-bank-template.json';
    anchor.click();
    URL.revokeObjectURL(url);
  }
  async function importFile(file: File) {
    if (file.size > 2 * 1024 * 1024) {
      toast.error('الحد الأقصى للملف 2 ميجابايت');
      return;
    }
    setImporting(true);
    setError('');
    try {
      const payload: unknown = JSON.parse(await file.text());
      if (!Array.isArray(payload) || payload.length < 1 || payload.length > 100)
        throw new Error('يجب أن يحتوي الملف على قائمة من 1 إلى 100 سؤال.');
      const saved = await learningCenterService.importQuestions(
        payload.map((question) => ({
          ...question,
          lessonId: importLessonId,
        })) as QuestionInput[]
      );
      toast.success(`تم استيراد ${saved.length} سؤالًا`);
      refresh();
    } catch {
      setError(
        'تعذر تأكيد الاستيراد. راجع القالب والخيارات، وتحقق من البنك قبل إعادة المحاولة لتجنب التكرار.'
      );
    } finally {
      setImporting(false);
    }
  }
  return (
    <section className="space-y-5">
      <div className="flex flex-wrap gap-3">
        <button
          className="admin-btn-primary min-h-11"
          onClick={() => setEditor({})}
        >
          إضافة سؤال
        </button>
      </div>
      {editor && (
        <LearningQuestionEditor
          key={`${editor.question?.id ?? 'new'}:${editor.classify}`}
          question={editor.question}
          classificationOnly={editor.classify}
          options={options}
          onSaved={refresh}
          onClose={() => setEditor(null)}
        />
      )}
      <details>
        <summary className="cursor-pointer text-sm">
          استيراد أسئلة من ملف
        </summary>
        <div className="mt-3 flex flex-wrap items-end gap-3">
          <label>
            الدرس الذي ستُضاف إليه الأسئلة
            <select
              className="admin-input mt-1"
              value={importLessonId}
              onChange={(e) => setImportLessonId(e.target.value)}
              disabled={importing}
            >
              <option value="">اختر الدرس</option>
              {options.lessons.map((lesson) => (
                <option key={lesson.id} value={lesson.id}>
                  {
                    options.packages.find((p) => p.id === lesson.packageId)
                      ?.name
                  }{' '}
                  / {lesson.title}
                </option>
              ))}
            </select>
          </label>
          <button className="admin-btn-ghost min-h-11" onClick={template}>
            تنزيل قالب الاستيراد
          </button>
          <label className="admin-btn-ghost min-h-11 cursor-pointer">
            {importing ? 'جارٍ الاستيراد…' : 'استيراد ملف JSON'}
            <input
              aria-label="استيراد أسئلة من ملف JSON"
              type="file"
              accept=".json,application/json"
              className="sr-only"
              disabled={importing || !importLessonId}
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) void importFile(file);
                e.target.value = '';
              }}
            />
          </label>
        </div>
        <p className="mt-2 text-sm text-[var(--admin-muted)]">
          اختر الدرس، ثم عبّئ القالب وارفع الملف. الحد الأقصى 100 سؤال و2
          ميجابايت.
        </p>
      </details>
      <div className="grid gap-3 md:grid-cols-3">
        <select
          aria-label="مدرّس بنك الأسئلة"
          className="admin-input"
          value={filter.teacherId}
          onChange={(e) =>
            setFilter({
              ...filter,
              teacherId: e.target.value,
              packageId: '',
              lessonId: '',
              page: 1,
            })
          }
        >
          <option value="">كل المدرسين المتاحين</option>
          {[
            ...new Map(
              options.packages.map((p) => [p.teacherId, p.teacherName])
            ).entries(),
          ].map(([id, name]) => (
            <option key={id} value={id}>
              {name}
            </option>
          ))}
        </select>
        <select
          aria-label="مادة بنك الأسئلة"
          className="admin-input"
          value={filter.subjectId}
          onChange={(e) =>
            setFilter({
              ...filter,
              subjectId: e.target.value,
              packageId: '',
              lessonId: '',
              page: 1,
            })
          }
        >
          <option value="">كل المواد</option>
          {[
            ...new Map(
              options.packages.map((p) => [p.subjectId, p.subjectName])
            ).entries(),
          ].map(([id, name]) => (
            <option key={id} value={id}>
              {name}
            </option>
          ))}
        </select>
        <input
          aria-label="البحث في الأسئلة والأفكار"
          className="admin-input"
          placeholder="ابحث في السؤال أو الفكرة"
          value={filter.search}
          onChange={(e) =>
            setFilter({ ...filter, search: e.target.value, page: 1 })
          }
        />
        <select
          aria-label="كورس بنك الأسئلة"
          className="admin-input"
          value={filter.packageId}
          onChange={(e) =>
            setFilter({
              ...filter,
              packageId: e.target.value,
              lessonId: '',
              page: 1,
            })
          }
        >
          <option value="">كل الكورسات والأسئلة غير المصنفة</option>
          {options.packages.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
        <select
          aria-label="درس بنك الأسئلة"
          className="admin-input"
          value={filter.lessonId}
          onChange={(e) =>
            setFilter({ ...filter, lessonId: e.target.value, page: 1 })
          }
        >
          <option value="">كل الدروس</option>
          {options.lessons
            .filter(
              (l) => !filter.packageId || l.packageId === filter.packageId
            )
            .map((l) => (
              <option key={l.id} value={l.id}>
                {l.title}
              </option>
            ))}
        </select>
        <select
          aria-label="مستوى صعوبة الأسئلة"
          className="admin-input"
          value={filter.difficulty}
          onChange={(e) =>
            setFilter({ ...filter, difficulty: e.target.value, page: 1 })
          }
        >
          <option value="">كل مستويات الصعوبة</option>
          <option value="0">غير مصنف</option>
          <option value="1">سهل</option>
          <option value="2">متوسط</option>
          <option value="3">صعب</option>
        </select>
      </div>
      {error && (
        <p role="alert">
          {error}{' '}
          <button
            className="admin-btn-ghost"
            onClick={() => setRevision(revision + 1)}
          >
            إعادة تحميل البنك
          </button>
        </p>
      )}
      {!questions && !error && <p role="status">جارٍ تحميل الأسئلة…</p>}
      {questions && (
        <>
          <p className="text-sm">
            {questions.totalCount} سؤالًا مطابقًا. يظهر تحليل جودة الأسئلة
            المستخدمة داخل خريطة الفهم.
          </p>
          <div className="divide-y divide-[var(--admin-border)]">
            {questions.items.map((q) => (
              <article
                key={q.id}
                className="flex flex-wrap items-start justify-between gap-4 py-4"
              >
                <div className="min-w-0 flex-1">
                  <h3 className="break-words font-bold">
                    {questionTextToPlainText(q.text)}
                  </h3>
                  <p className="mt-1 text-sm text-[var(--admin-muted)]">
                    {q.concept || 'يحتاج تصنيفًا'} ·{' '}
                    {['غير مصنف', 'سهل', 'متوسط', 'صعب'][q.difficulty]} ·{' '}
                    {q.points} درجات
                  </p>
                  <details className="mt-2 text-sm">
                    <summary className="cursor-pointer">
                      عرض الاختيارات والشرح
                    </summary>
                    <ul className="mt-2 space-y-1">
                      {q.options.map((o, i) => (
                        <li key={o.id ?? i}>
                          {questionTextToPlainText(o.text)}{' '}
                          {o.isCorrect && <strong>(الإجابة الصحيحة)</strong>}
                        </li>
                      ))}
                    </ul>
                    {q.correction && (
                      <p className="mt-3">
                        {questionTextToPlainText(q.correction)}
                      </p>
                    )}
                  </details>
                </div>
                <div className="flex gap-2">
                  <button
                    className="admin-btn-ghost min-h-11"
                    onClick={() => setEditor({ question: q, classify: true })}
                  >
                    تصنيف السؤال
                  </button>
                  {q.type === 0 && (
                    <button
                      className="admin-btn-ghost min-h-11"
                      onClick={() => setEditor({ question: q })}
                    >
                      تعديل بنسخة جديدة
                    </button>
                  )}
                </div>
              </article>
            ))}
          </div>
          {questions.items.length === 0 && (
            <p className="p-8 text-center">
              لا توجد أسئلة مطابقة. أضف سؤالًا أو غيّر الفلاتر.
            </p>
          )}
          <div className="flex items-center gap-3">
            <button
              className="admin-btn-ghost"
              disabled={filter.page === 1}
              onClick={() => setFilter({ ...filter, page: filter.page - 1 })}
            >
              السابق
            </button>
            <span>
              صفحة {filter.page} من{' '}
              {Math.max(1, Math.ceil(questions.totalCount / 20))}
            </span>
            <button
              className="admin-btn-ghost"
              disabled={filter.page * 20 >= questions.totalCount}
              onClick={() => setFilter({ ...filter, page: filter.page + 1 })}
            >
              التالي
            </button>
          </div>
        </>
      )}
    </section>
  );
}

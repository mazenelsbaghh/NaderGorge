'use client';

import { useState } from 'react';
import toast from 'react-hot-toast';
import {
  learningCenterService,
  type LearningOptions,
  type LearningQuestion,
  type QuestionInput,
} from '@/services/learning-center-service';

export function LearningQuestionEditor({
  question,
  options,
  onSaved,
  onClose,
  classificationOnly = false,
}: {
  question?: LearningQuestion;
  options: LearningOptions;
  onSaved: () => void;
  onClose: () => void;
  classificationOnly?: boolean;
}) {
  const [form, setForm] = useState<QuestionInput>({
    text: question?.text ?? '',
    lessonId: question?.lessonId ?? '',
    concept: question?.concept ?? '',
    difficulty: question?.difficulty || 2,
    points: question?.points ?? 1,
    tags: question?.tags ?? '',
    correction: question?.correction ?? '',
    options: question?.options.map((o) => ({
      text: o.text,
      isCorrect: o.isCorrect,
    })) ?? [
      { text: '', isCorrect: true },
      { text: '', isCorrect: false },
      { text: '', isCorrect: false },
      { text: '', isCorrect: false },
    ],
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const patch = (next: Partial<QuestionInput>) =>
    setForm((current) => ({ ...current, ...next }));
  const eligiblePackages = options.packages.filter(
    (p) =>
      !question ||
      (p.teacherId === question.teacherId && p.subjectId === question.subjectId)
  );
  const lessons = options.lessons.filter((l) =>
    eligiblePackages.some((p) => p.id === l.packageId)
  );
  async function save() {
    setSaving(true);
    setError('');
    try {
      if (classificationOnly && question)
        await learningCenterService.classify(question.id, {
          lessonId: form.lessonId,
          concept: form.concept,
          difficulty: form.difficulty,
        });
      else
        await learningCenterService.saveQuestion(
          { ...form, options: form.options.filter((o) => o.text.trim()) },
          question?.id
        );
      toast.success('تم حفظ السؤال');
      onSaved();
    } catch {
      setError('تعذر حفظ السؤال. راجع الدرس والخيارات ثم حاول مرة أخرى.');
    } finally {
      setSaving(false);
    }
  }
  return (
    <form
      className="space-y-4 rounded-xl border border-[var(--admin-border)] p-5"
      onSubmit={(e) => {
        e.preventDefault();
        void save();
      }}
    >
      <h3 className="text-xl font-bold">
        {classificationOnly
          ? 'تصنيف السؤال'
          : question
            ? 'إنشاء نسخة معدّلة'
            : 'إضافة سؤال'}
      </h3>
      {question && !classificationOnly && (
        <p className="text-sm">
          سيُحفظ التعديل كنسخة جديدة. الامتحانات المرتبطة بالسؤال الحالي تحتفظ
          بنسختها.
        </p>
      )}
      <div className="grid gap-3 md:grid-cols-3">
        <label>
          الدرس
          <select
            className="admin-input mt-1"
            required
            value={form.lessonId}
            onChange={(e) => patch({ lessonId: e.target.value })}
          >
            <option value="">اختر الدرس</option>
            {lessons.map((l) => (
              <option key={l.id} value={l.id}>
                {eligiblePackages.find((p) => p.id === l.packageId)?.name} /{' '}
                {l.title}
              </option>
            ))}
          </select>
        </label>
        <label>
          الفكرة
          <input
            className="admin-input mt-1"
            required
            maxLength={160}
            value={form.concept}
            onChange={(e) => patch({ concept: e.target.value })}
          />
        </label>
        <label>
          الصعوبة المتوقعة
          <select
            className="admin-input mt-1"
            value={form.difficulty}
            onChange={(e) => patch({ difficulty: Number(e.target.value) })}
          >
            <option value={1}>سهل</option>
            <option value={2}>متوسط</option>
            <option value={3}>صعب</option>
          </select>
        </label>
      </div>
      {!classificationOnly && (
        <>
          <label className="block">
            نص السؤال
            <textarea
              className="admin-input mt-1"
              required
              rows={3}
              maxLength={10000}
              value={form.text}
              onChange={(e) => patch({ text: e.target.value })}
            />
          </label>
          <div className="grid gap-3 md:grid-cols-2">
            <label>
              الدرجة
              <input
                className="admin-input mt-1"
                required
                type="number"
                min={0.5}
                max={100}
                step={0.5}
                value={form.points}
                onChange={(e) => patch({ points: Number(e.target.value) })}
              />
            </label>
            <label>
              وسوم إضافية
              <input
                className="admin-input mt-1"
                maxLength={1000}
                value={form.tags}
                onChange={(e) => patch({ tags: e.target.value })}
              />
            </label>
          </div>
          <fieldset className="space-y-2">
            <legend className="mb-2 font-bold">
              الاختيارات، حدد إجابة صحيحة واحدة
            </legend>
            {form.options.map((option, index) => (
              <div key={index} className="flex items-center gap-3">
                <input
                  type="radio"
                  name="correct-answer"
                  aria-label={`الاختيار الصحيح ${index + 1}`}
                  checked={option.isCorrect}
                  onChange={() =>
                    patch({
                      options: form.options.map((o, i) => ({
                        ...o,
                        isCorrect: i === index,
                      })),
                    })
                  }
                />
                <input
                  aria-label={`نص الاختيار ${index + 1}`}
                  className="admin-input"
                  maxLength={4000}
                  required={index < 2 || option.isCorrect}
                  value={option.text}
                  onChange={(e) =>
                    patch({
                      options: form.options.map((o, i) =>
                        i === index ? { ...o, text: e.target.value } : o
                      ),
                    })
                  }
                />
              </div>
            ))}
          </fieldset>
          <label className="block">
            شرح الإجابة
            <textarea
              className="admin-input mt-1"
              rows={2}
              maxLength={10000}
              value={form.correction ?? ''}
              onChange={(e) => patch({ correction: e.target.value })}
            />
          </label>
        </>
      )}
      {error && <p role="alert">{error}</p>}
      <div className="flex gap-3">
        <button className="admin-btn-primary min-h-11" disabled={saving}>
          {saving ? 'جارٍ الحفظ…' : 'حفظ السؤال'}
        </button>
        <button
          type="button"
          className="admin-btn-ghost min-h-11"
          onClick={onClose}
          disabled={saving}
        >
          إلغاء
        </button>
      </div>
    </form>
  );
}

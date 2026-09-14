'use client';

import Link from 'next/link';
import { useState } from 'react';
import {
  learningCenterService,
  type BlueprintRow,
  type GeneratedForm,
  type LearningOptions,
} from '@/services/learning-center-service';

export function LearningForms({
  options,
  mode,
}: {
  options: LearningOptions;
  mode: 'admin' | 'teacher';
}) {
  const [form, setForm] = useState({
    packageId: '',
    title: '',
    forms: 2,
    durationMinutes: 30,
    passingPercent: 60,
    blueprint: [
      { lessonId: '', concept: '', difficulty: 2, count: 5 },
    ] as BlueprintRow[],
  });
  const [requestId, setRequestId] = useState('');
  const [generated, setGenerated] = useState<GeneratedForm[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const patch = (next: Partial<typeof form>) => {
    setForm((current) => ({ ...current, ...next }));
    setRequestId('');
    setGenerated([]);
  };
  const patchRow = (index: number, next: Partial<BlueprintRow>) =>
    patch({
      blueprint: form.blueprint.map((row, i) =>
        i === index ? { ...row, ...next } : row
      ),
    });
  async function generate() {
    const id = requestId || crypto.randomUUID();
    setRequestId(id);
    setSaving(true);
    setError('');
    try {
      setGenerated(
        await learningCenterService.generate({ ...form, requestId: id })
      );
    } catch {
      setError(
        'تعذر إنشاء النماذج. تأكد من وجود أسئلة كافية لكل فكرة وصعوبة، وبنفس الدرجات بين النماذج.'
      );
    } finally {
      setSaving(false);
    }
  }
  return (
    <section className="space-y-5">
      <p className="text-sm text-[var(--admin-muted)]">
        كل نموذج له نفس توزيع الأفكار والصعوبة والدرجات، بأسئلة مختلفة. الصعوبة
        الفعلية تحتاج تحليل النتائج بعد الاستخدام. تُنشأ النماذج غير مفعّلة
        للمراجعة وربطها بالمحتوى.
      </p>
      <form
        className="space-y-5"
        onSubmit={(e) => {
          e.preventDefault();
          void generate();
        }}
      >
        <fieldset
          disabled={saving || generated.length > 0}
          className="space-y-5"
        >
          <div className="grid gap-3 md:grid-cols-2">
            <label>
              الكورس
              <select
                required
                className="admin-input mt-1"
                value={form.packageId}
                onChange={(e) =>
                  patch({
                    packageId: e.target.value,
                    blueprint: [
                      { lessonId: '', concept: '', difficulty: 2, count: 5 },
                    ],
                  })
                }
              >
                <option value="">اختر الكورس</option>
                {options.packages.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name} · {p.teacherName}
                  </option>
                ))}
              </select>
            </label>
            <label>
              اسم مجموعة النماذج
              <input
                className="admin-input mt-1"
                required
                maxLength={180}
                value={form.title}
                onChange={(e) => patch({ title: e.target.value })}
              />
            </label>
          </div>
          <div className="grid gap-3 md:grid-cols-3">
            <label>
              عدد النماذج
              <input
                className="admin-input mt-1"
                type="number"
                min={1}
                max={5}
                required
                value={form.forms}
                onChange={(e) => patch({ forms: Number(e.target.value) })}
              />
            </label>
            <label>
              مدة النموذج بالدقائق
              <input
                className="admin-input mt-1"
                type="number"
                min={1}
                max={180}
                required
                value={form.durationMinutes}
                onChange={(e) =>
                  patch({ durationMinutes: Number(e.target.value) })
                }
              />
            </label>
            <label>
              نسبة النجاح
              <input
                className="admin-input mt-1"
                type="number"
                min={1}
                max={100}
                required
                value={form.passingPercent}
                onChange={(e) =>
                  patch({ passingPercent: Number(e.target.value) })
                }
              />
            </label>
          </div>
          <h3 className="font-bold">توزيع الأسئلة في كل نموذج</h3>
          {form.blueprint.map((row, index) => (
            <div
              key={index}
              className="grid items-end gap-3 rounded-xl bg-[var(--admin-card-soft)] p-4 md:grid-cols-[2fr_2fr_1fr_1fr_auto]"
            >
              <label>
                الدرس
                <select
                  required
                  className="admin-input mt-1"
                  value={row.lessonId}
                  onChange={(e) =>
                    patchRow(index, { lessonId: e.target.value, concept: '' })
                  }
                >
                  <option value="">اختر الدرس</option>
                  {options.lessons
                    .filter((l) => l.packageId === form.packageId)
                    .map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.title}
                      </option>
                    ))}
                </select>
              </label>
              <label>
                الفكرة
                <select
                  required
                  className="admin-input mt-1"
                  value={row.concept}
                  onChange={(e) => patchRow(index, { concept: e.target.value })}
                >
                  <option value="">اختر الفكرة</option>
                  {options.concepts
                    .filter((choice) => choice.lessonId === row.lessonId)
                    .map((choice) => (
                      <option key={choice.concept} value={choice.concept}>
                        {choice.concept}
                      </option>
                    ))}
                </select>
              </label>
              <label>
                الصعوبة
                <select
                  className="admin-input mt-1"
                  value={row.difficulty}
                  onChange={(e) =>
                    patchRow(index, { difficulty: Number(e.target.value) })
                  }
                >
                  <option value={1}>سهل</option>
                  <option value={2}>متوسط</option>
                  <option value={3}>صعب</option>
                </select>
              </label>
              <label>
                عدد الأسئلة
                <input
                  required
                  className="admin-input mt-1"
                  type="number"
                  min={1}
                  max={50}
                  value={row.count}
                  onChange={(e) =>
                    patchRow(index, { count: Number(e.target.value) })
                  }
                />
              </label>
              <button
                type="button"
                className="admin-btn-ghost min-h-11"
                disabled={form.blueprint.length === 1}
                onClick={() =>
                  patch({
                    blueprint: form.blueprint.filter((_, i) => i !== index),
                  })
                }
              >
                حذف الصف
              </button>
            </div>
          ))}
          <button
            type="button"
            className="admin-btn-ghost min-h-11"
            disabled={form.blueprint.length >= 40}
            onClick={() =>
              patch({
                blueprint: [
                  ...form.blueprint,
                  { lessonId: '', concept: '', difficulty: 2, count: 5 },
                ],
              })
            }
          >
            إضافة فكرة للتوزيع
          </button>
          <p>
            سيتم إنشاء {form.forms} نماذج، في كل نموذج{' '}
            {form.blueprint.reduce((sum, row) => sum + row.count, 0)} سؤالًا.
          </p>
          <button
            className="admin-btn-primary min-h-11"
            disabled={
              saving ||
              form.blueprint.reduce((sum, row) => sum + row.count, 0) > 100
            }
          >
            {saving ? 'جارٍ إنشاء النماذج…' : 'إنشاء النماذج للمراجعة'}
          </button>
        </fieldset>
      </form>
      {error && <p role="alert">{error}</p>}
      {generated.length > 0 && (
        <div role="status" className="space-y-3">
          <h3 className="text-lg font-bold">تم إنشاء النماذج</h3>
          <ul className="space-y-3">
            {generated.map((exam) => (
              <li key={exam.examId}>
                <Link
                  className="underline underline-offset-4"
                  href={
                    mode === 'admin'
                      ? `/admin/content/exams/${exam.examId}`
                      : `/teacher/packages/exams/${exam.examId}`
                  }
                >
                  {exam.title}
                </Link>{' '}
                · {exam.questions} أسئلة · {exam.totalScore} درجات
              </li>
            ))}
          </ul>
          <button
            className="admin-btn-ghost min-h-11"
            onClick={() => {
              setGenerated([]);
              setRequestId('');
            }}
          >
            إعداد مجموعة أخرى
          </button>
        </div>
      )}
    </section>
  );
}

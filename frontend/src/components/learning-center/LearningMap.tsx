'use client';

import { useEffect, useState } from 'react';
import {
  learningCenterService,
  type LearningFilter,
  type LearningOverview,
} from '@/services/learning-center-service';
import { questionTextToPlainText } from '@/lib/question-text';
import { isRequestCancellation } from '@/services/api-client';

export function LearningMap({ filter }: { filter: LearningFilter }) {
  const [overview, setOverview] = useState<LearningOverview | null>(null);
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setOverview(null);
    setError('');
    learningCenterService
      .overview(filter, controller.signal)
      .then(setOverview)
      .catch((failure: unknown) => {
        if (!isRequestCancellation(failure))
          setError(
            'تعذر تحميل الخريطة. جرّب فترة أقصر أو كورسًا محددًا، ثم أعد المحاولة.'
          );
      });
    return () => controller.abort();
  }, [filter, retry]);
  if (error)
    return (
      <div role="alert">
        {error}{' '}
        <button className="admin-btn-ghost" onClick={() => setRetry(retry + 1)}>
          إعادة المحاولة
        </button>
      </div>
    );
  if (!overview)
    return (
      <p role="status" className="p-8">
        جارٍ تحليل الإجابات…
      </p>
    );
  return (
    <section className="space-y-5" aria-label="خريطة فهم المنهج">
      <p className="text-sm text-[var(--admin-muted)]">
        تعتمد النسب على آخر إجابة لكل طالب على كل سؤال، بحد أدنى{' '}
        {overview.minimumStudents} طلاب. التصنيف الحالي للأسئلة يُستخدم لتنظيم
        النتائج السابقة. تم استبعاد {overview.excludedAttempts} محاولة غير
        مكتملة أو غير صالحة للتحليل.
      </p>
      {overview.unclassifiedQuestions > 0 && (
        <p className="rounded-xl bg-[var(--admin-card-soft)] p-4">
          هناك {overview.unclassifiedQuestions} سؤالًا لدى المدرسين المختارين
          يحتاج ربطه بدرس وفكرة من تبويب بنك الأسئلة.
        </p>
      )}
      {overview.concepts.length === 0 && (
        <p className="p-8 text-center">
          اربط الأسئلة بالدروس والأفكار من البنك لتظهر خريطة الفهم هنا.
        </p>
      )}
      <div className="divide-y divide-[var(--admin-border)]">
        {overview.concepts.map((concept) => (
          <details
            key={`${concept.lessonId}:${concept.concept}`}
            className="py-4"
          >
            <summary className="cursor-pointer rounded-lg p-2 focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)]">
              <span className="inline-flex w-[94%] flex-wrap items-center justify-between gap-3 align-middle">
                <span>
                  <strong className="block text-lg">{concept.concept}</strong>
                  <span className="text-sm text-[var(--admin-muted)]">
                    {concept.package} / {concept.lesson}
                  </span>
                </span>
                <span className="flex items-center gap-4">
                  <span className="text-sm">
                    {concept.students} طلاب · {concept.attempts} محاولات
                  </span>
                  <strong>
                    {concept.correctPercent === null
                      ? 'بيانات غير كافية'
                      : `${concept.correctPercent}% إجابات صحيحة`}
                  </strong>
                </span>
              </span>
              {concept.correctPercent !== null && (
                <meter
                  aria-label={`فهم ${concept.concept}`}
                  min={0}
                  max={100}
                  low={60}
                  high={80}
                  optimum={100}
                  value={concept.correctPercent}
                  className="mt-3 h-3 w-full"
                />
              )}
            </summary>
            <div className="mt-4 space-y-5 pr-4">
              <div className="overflow-x-auto">
                <table className="w-full text-right text-sm">
                  <caption className="mb-3 text-right font-bold">
                    الأسئلة التي تكشف نقاط الضعف
                  </caption>
                  <thead>
                    <tr className="bg-[var(--admin-card-soft)]">
                      <th className="p-3">السؤال</th>
                      <th className="p-3">الطلاب / الإجابات</th>
                      <th className="p-3">الصحيح</th>
                      <th className="p-3">أكثر اختيار خاطئ</th>
                      <th className="p-3">فرق أداء المستويات</th>
                    </tr>
                  </thead>
                  <tbody>
                    {concept.questions.map((q) => (
                      <tr
                        key={q.questionId}
                        className="border-b border-[var(--admin-border)]"
                      >
                        <td className="min-w-48 max-w-lg p-3 break-words">
                          {questionTextToPlainText(q.text)}
                        </td>
                        <td className="p-3">
                          {q.students} / {q.attempts}
                        </td>
                        <td className="p-3">
                          {q.correctPercent === null
                            ? 'بيانات غير كافية'
                            : `${q.correctPercent}%`}
                        </td>
                        <td className="p-3">
                          {q.commonWrongAnswer
                            ? `${questionTextToPlainText(q.commonWrongAnswer)} (${q.commonWrongCount})`
                            : 'لا يوجد اختيار خاطئ مسجل'}
                        </td>
                        <td className="p-3">
                          {q.discrimination === null
                            ? 'بيانات غير كافية'
                            : `${q.discrimination} نقطة`}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <p className="text-xs text-[var(--admin-muted)]">
                فرق أداء المستويات: الفرق بين نسبة الإجابة الصحيحة لدى الثلث
                الأعلى والثلث الأقل في نتائج الامتحان، عند وجود 10 طلاب على
                الأقل. القيمة السالبة تستدعي مراجعة السؤال.
              </p>
              <div className="grid gap-5 lg:grid-cols-2">
                <div>
                  <h3 className="mb-2 font-bold">طلاب يحتاجون مراجعة الفكرة</h3>
                  {concept.studentsNeedingReview.length ? (
                    <ul className="space-y-2">
                      {concept.studentsNeedingReview.map((s) => (
                        <li
                          key={s.studentId}
                          className="flex justify-between gap-3"
                        >
                          <span>{s.name}</span>
                          <span>{s.correctPercent}%</span>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p>لا توجد نتائج أقل من 60%.</p>
                  )}
                </div>
                <div>
                  <h3 className="mb-2 font-bold">تطور النتائج حسب اليوم</h3>
                  <ul className="space-y-2">
                    {concept.trend.map((point) => (
                      <li
                        key={point.date}
                        className="flex justify-between gap-3"
                      >
                        <time>{point.date}</time>
                        <span>{point.students} طلاب</span>
                        <span>
                          {point.correctPercent === null
                            ? 'بيانات غير كافية'
                            : `${point.correctPercent}%`}
                        </span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </div>
          </details>
        ))}
      </div>
    </section>
  );
}

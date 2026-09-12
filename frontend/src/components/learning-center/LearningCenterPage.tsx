'use client';

import { useEffect, useState } from 'react';
import { AdminPage } from '@/components/admin';
import { TeacherPage } from '@/components/teacher/TeacherShellChrome';
import {
  learningCenterService,
  type LearningFilter,
  type LearningOptions,
} from '@/services/learning-center-service';
import { isRequestCancellation } from '@/services/api-client';
import { LearningMap } from './LearningMap';
import { LearningFollowUpList } from './LearningFollowUpList';
import { LearningBank } from './LearningBank';
import { LearningForms } from './LearningForms';

const tabs = [
  { id: 'map', label: 'خريطة الفهم' },
  { id: 'followup', label: 'طلاب يحتاجون متابعة' },
  { id: 'bank', label: 'بنك الأسئلة' },
  { id: 'forms', label: 'نماذج الامتحانات' },
] as const;
type Tab = (typeof tabs)[number]['id'];
const initialFilter: LearningFilter = {
  days: 30,
  inactiveDays: 7,
  declinePoints: 15,
  repeatedAttempts: 3,
};

export default function LearningCenterPage({
  mode,
}: {
  mode: 'admin' | 'teacher';
}) {
  const [tab, setTab] = useState<Tab>('map');
  const [options, setOptions] = useState<LearningOptions | null>(null);
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);
  const [draft, setDraft] = useState(initialFilter);
  const [filter, setFilter] = useState(initialFilter);
  useEffect(() => {
    const controller = new AbortController();
    setError('');
    learningCenterService
      .options(controller.signal)
      .then(setOptions)
      .catch((failure: unknown) => {
        if (!isRequestCancellation(failure))
          setError(
            'تعذر فتح مركز التقييم. تحقق من صلاحيات الوصول ثم أعد المحاولة.'
          );
      });
    return () => controller.abort();
  }, [retry]);
  const teachers = [
    ...new Map(
      options?.packages.map((p) => [p.teacherId, p.teacherName])
    ).entries(),
  ];
  const subjects = [
    ...new Map(
      options?.packages.map((p) => [p.subjectId, p.subjectName])
    ).entries(),
  ];
  const grades = [
    ...new Set(options?.packages.map((p) => p.grade).filter(Boolean)),
  ];
  const content = (
    <div className="space-y-6" dir="rtl">
      <nav
        aria-label="أقسام مركز التقييم"
        className="flex flex-wrap gap-2 border-b border-[var(--admin-border)] pb-4"
      >
        {tabs.map((item) => (
          <button
            key={item.id}
            aria-current={tab === item.id ? 'page' : undefined}
            className={`${tab === item.id ? 'admin-btn-primary' : 'admin-btn-ghost'} min-h-11`}
            onClick={() => setTab(item.id)}
          >
            {item.label}
          </button>
        ))}
      </nav>
      {error && (
        <p role="alert">
          {error}{' '}
          <button
            className="admin-btn-ghost min-h-11"
            onClick={() => setRetry(retry + 1)}
          >
            إعادة المحاولة
          </button>
        </p>
      )}
      {!options && !error && (
        <div
          role="status"
          className="rounded-xl bg-[var(--admin-card-soft)] p-10"
        >
          جارٍ تحميل الكورسات والدروس…
        </div>
      )}
      {options && (
        <>
          {(tab === 'map' || tab === 'followup') && (
            <form
              className="space-y-4 rounded-xl bg-[var(--admin-card-soft)] p-4"
              onSubmit={(e) => {
                e.preventDefault();
                setFilter({ ...draft });
              }}
            >
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
                {mode === 'admin' && (
                  <label>
                    المدرّس
                    <select
                      className="admin-input mt-1"
                      value={draft.teacherId ?? ''}
                      onChange={(e) =>
                        setDraft({
                          ...draft,
                          teacherId: e.target.value || undefined,
                          packageId: undefined,
                        })
                      }
                    >
                      <option value="">كل المدرسين</option>
                      {teachers.map(([id, name]) => (
                        <option key={id} value={id}>
                          {name}
                        </option>
                      ))}
                    </select>
                  </label>
                )}
                <label>
                  المادة
                  <select
                    className="admin-input mt-1"
                    value={draft.subjectId ?? ''}
                    onChange={(e) =>
                      setDraft({
                        ...draft,
                        subjectId: e.target.value || undefined,
                        packageId: undefined,
                      })
                    }
                  >
                    <option value="">كل المواد</option>
                    {subjects.map(([id, name]) => (
                      <option key={id} value={id}>
                        {name}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  الصف
                  <select
                    className="admin-input mt-1"
                    value={draft.grade ?? ''}
                    onChange={(e) =>
                      setDraft({
                        ...draft,
                        grade: e.target.value || undefined,
                        packageId: undefined,
                      })
                    }
                  >
                    <option value="">كل الصفوف</option>
                    {grades.map((grade) => (
                      <option key={grade} value={grade}>
                        {grade}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  الكورس
                  <select
                    className="admin-input mt-1"
                    value={draft.packageId ?? ''}
                    onChange={(e) =>
                      setDraft({
                        ...draft,
                        packageId: e.target.value || undefined,
                      })
                    }
                  >
                    <option value="">كل الكورسات</option>
                    {options.packages
                      .filter(
                        (p) =>
                          (!draft.teacherId ||
                            p.teacherId === draft.teacherId) &&
                          (!draft.subjectId ||
                            p.subjectId === draft.subjectId) &&
                          (!draft.grade || p.grade === draft.grade)
                      )
                      .map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  فترة النتائج
                  <select
                    className="admin-input mt-1"
                    value={draft.days}
                    onChange={(e) =>
                      setDraft({ ...draft, days: Number(e.target.value) })
                    }
                  >
                    <option value={7}>آخر 7 أيام</option>
                    <option value={30}>آخر 30 يومًا</option>
                    <option value={90}>آخر 90 يومًا</option>
                    <option value={365}>آخر سنة</option>
                  </select>
                </label>
              </div>
              {tab === 'followup' && (
                <fieldset className="grid gap-3 md:grid-cols-3">
                  <legend className="mb-2 font-bold">
                    حدود تنبيهات المتابعة
                  </legend>
                  <label>
                    التوقف عن المذاكرة بالأيام
                    <input
                      className="admin-input mt-1"
                      type="number"
                      required
                      min={1}
                      max={90}
                      value={draft.inactiveDays}
                      onChange={(e) =>
                        setDraft({
                          ...draft,
                          inactiveDays: Number(e.target.value),
                        })
                      }
                    />
                  </label>
                  <label>
                    انخفاض النتيجة بالنقاط المئوية
                    <input
                      className="admin-input mt-1"
                      type="number"
                      required
                      min={1}
                      max={100}
                      value={draft.declinePoints}
                      onChange={(e) =>
                        setDraft({
                          ...draft,
                          declinePoints: Number(e.target.value),
                        })
                      }
                    />
                  </label>
                  <label>
                    محاولات متكررة دون تحسن
                    <input
                      className="admin-input mt-1"
                      type="number"
                      required
                      min={2}
                      max={10}
                      value={draft.repeatedAttempts}
                      onChange={(e) =>
                        setDraft({
                          ...draft,
                          repeatedAttempts: Number(e.target.value),
                        })
                      }
                    />
                  </label>
                </fieldset>
              )}
              <button className="admin-btn-primary min-h-11">
                تطبيق الفلاتر
              </button>
            </form>
          )}
          {tab === 'map' && <LearningMap filter={filter} />}
          {tab === 'followup' && <LearningFollowUpList filter={filter} />}
          {tab === 'bank' && (
            <LearningBank
              options={options}
              onChanged={() => setRetry((current) => current + 1)}
            />
          )}
          {tab === 'forms' && <LearningForms options={options} mode={mode} />}
        </>
      )}
    </div>
  );
  return mode === 'admin' ? (
    <AdminPage
      activePath="/admin/learning-center"
      sectionLabel="التعليم"
      pageTitle="مركز التقييم والمتابعة"
      subtitle="اعرف نقاط الضعف، ونظّم التقييم، وتابع أثر التدخل."
    >
      {content}
    </AdminPage>
  ) : (
    <TeacherPage
      activePath="/teacher/learning-center"
      sectionLabel="التعليم"
      pageTitle="مركز التقييم والمتابعة"
      subtitle="فهم طلابك ونتائجهم، في مساحة مرتبطة بكورساتك."
    >
      {content}
    </TeacherPage>
  );
}

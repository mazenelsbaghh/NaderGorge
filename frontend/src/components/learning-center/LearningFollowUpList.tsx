'use client';

import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { formatDate } from '@/components/admin/admin-utils';
import {
  learningCenterService,
  type FollowUpStatus,
  type LearningFilter,
  type LearningFollowUp,
  type LearningHistory,
} from '@/services/learning-center-service';
import { isRequestCancellation } from '@/services/api-client';
const statuses: Record<FollowUpStatus, string> = {
  New: 'جديد',
  InProgress: 'جارٍ المتابعة',
  Completed: 'تمت المتابعة',
};
const date = (stamp: string | null) =>
  stamp
    ? formatDate(stamp)
    : 'لم يسجل نشاطًا بعد';

export function LearningFollowUpList({ filter }: { filter: LearningFilter }) {
  const [students, setStudents] = useState<LearningFollowUp[] | null>(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [reason, setReason] = useState('');
  const [page, setPage] = useState(1);
  useEffect(() => {
    const controller = new AbortController();
    setStudents(null);
    setError('');
    setPage(1);
    learningCenterService
      .followUps(filter, controller.signal)
      .then(setStudents)
      .catch((failure: unknown) => {
        if (!isRequestCancellation(failure))
          setError(
            'تعذر تحميل المتابعة. اختر كورسًا أو فترة أقصر ثم أعد المحاولة.'
          );
      });
    return () => controller.abort();
  }, [filter, revision]);
  if (error)
    return (
      <p role="alert">
        {error}{' '}
        <button
          onClick={() => setRevision(revision + 1)}
          className="admin-btn-ghost"
        >
          إعادة المحاولة
        </button>
      </p>
    );
  if (!students)
    return (
      <p role="status" className="p-8">
        جارٍ مراجعة نشاط الطلاب…
      </p>
    );
  const filtered = students.filter(
    (s) =>
      s.name.includes(search) &&
      (!status || s.status === status) &&
      (!reason || s.reasons.some((r) => r.includes(reason)))
  );
  return (
    <section className="space-y-5">
      <p className="text-sm text-[var(--admin-muted)]">
        المتابعة للطلاب أصحاب الاشتراكات السارية. انخفاض الدرجات وتكرار
        المحاولات يُقاسان على نفس نسخة التقييم. إتمام المتابعة لا يخفي التنبيه
        طالما سببه مستمر.
      </p>
      <div className="flex flex-wrap gap-3">
        <input
          aria-label="ابحث باسم الطالب"
          className="admin-input max-w-xs"
          placeholder="ابحث باسم الطالب"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
        <select
          aria-label="حالة المتابعة"
          className="admin-input max-w-48"
          value={status}
          onChange={(e) => {
            setStatus(e.target.value);
            setPage(1);
          }}
        >
          <option value="">كل الحالات</option>
          {Object.entries(statuses).map(([key, label]) => (
            <option key={key} value={key}>
              {label}
            </option>
          ))}
        </select>
        <select
          aria-label="سبب المتابعة"
          className="admin-input max-w-48"
          value={reason}
          onChange={(e) => {
            setReason(e.target.value);
            setPage(1);
          }}
        >
          <option value="">كل الأسباب</option>
          <option value="نشاط">توقف عن المذاكرة</option>
          <option value="انخفضت">انخفاض النتيجة</option>
          <option value="كرر">محاولات دون تحسن</option>
        </select>
        <span className="self-center">{filtered.length} حالة</span>
      </div>
      {filtered.length === 0 && (
        <p className="p-8 text-center">
          لا توجد حالات متابعة مطابقة للحدود والفلاتر الحالية.
        </p>
      )}
      <div className="divide-y divide-[var(--admin-border)]">
        {filtered.slice((page - 1) * 20, page * 20).map((student) => (
          <FollowUpRow
            key={`${student.studentId}:${student.packageId}`}
            student={student}
            onSaved={() => setRevision(revision + 1)}
          />
        ))}
      </div>
      <div className="flex items-center gap-3">
        <button
          className="admin-btn-ghost"
          disabled={page === 1}
          onClick={() => setPage(page - 1)}
        >
          السابق
        </button>
        <span>
          صفحة {page} من {Math.max(1, Math.ceil(filtered.length / 20))}
        </span>
        <button
          className="admin-btn-ghost"
          disabled={page * 20 >= filtered.length}
          onClick={() => setPage(page + 1)}
        >
          التالي
        </button>
      </div>
    </section>
  );
}

function FollowUpRow({
  student,
  onSaved,
}: {
  student: LearningFollowUp;
  onSaved: () => void;
}) {
  const [status, setStatus] = useState(student.status);
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);
  const [history, setHistory] = useState<LearningHistory[] | null>(null);
  const [historyError, setHistoryError] = useState('');
  async function save() {
    setSaving(true);
    try {
      await learningCenterService.saveFollowUp({
        studentId: student.studentId,
        packageId: student.packageId,
        status,
        note,
        reason: student.reasons.join('، ') || 'متابعة سابقة',
      });
      toast.success('تم تسجيل المتابعة');
      onSaved();
    } catch {
      toast.error('تعذر حفظ المتابعة. ملاحظتك ما زالت موجودة، حاول مرة أخرى.');
    } finally {
      setSaving(false);
    }
  }
  async function loadHistory() {
    setHistoryError('');
    try {
      setHistory(await learningCenterService.history(student));
    } catch {
      setHistoryError('تعذر تحميل السجل. حاول مرة أخرى.');
    }
  }
  return (
    <details className="py-5">
      <summary className="cursor-pointer">
        <span className="inline-flex w-[94%] flex-wrap justify-between gap-3 align-middle">
          <span>
            <strong className="block text-lg">{student.name}</strong>
            <span className="text-sm text-[var(--admin-muted)]">
              {student.package} · {student.teacher}
            </span>
          </span>
          <span>
            {statuses[student.status]} · آخر نشاط:{' '}
            {date(student.lastActivityAt)}
          </span>
        </span>
        <span className="mt-3 block text-sm">
          {student.reasons.join(' · ') || 'لا توجد أسباب نشطة حاليًا'}
        </span>
      </summary>
      <div className="mt-4 space-y-4">
        <p>
          آخر نتيجة:{' '}
          {student.latestPercent === null
            ? 'غير متاحة'
            : `${student.latestPercent}%`}{' '}
          · التحسن بعد المتابعة:{' '}
          {student.improvementPoints === null
            ? 'لا توجد نتيجتان قابلتان للمقارنة بعد'
            : `${student.improvementPoints > 0 ? '+' : ''}${student.improvementPoints} نقطة`}
        </p>
        {student.note && (
          <p className="bg-[var(--admin-card-soft)] p-3">
            {student.note}
            <span className="mt-2 block text-xs">
              {student.followedUpBy} · {date(student.followedUpAt)}
            </span>
          </p>
        )}
        <form
          className="grid gap-3 md:grid-cols-[180px_1fr_auto]"
          onSubmit={(e) => {
            e.preventDefault();
            void save();
          }}
        >
          <select
            aria-label={`حالة متابعة ${student.name}`}
            className="admin-input"
            value={status}
            onChange={(e) => setStatus(e.target.value as FollowUpStatus)}
          >
            {Object.entries(statuses).map(([key, label]) => (
              <option key={key} value={key}>
                {label}
              </option>
            ))}
          </select>
          <textarea
            aria-label={`ملاحظة متابعة ${student.name}`}
            className="admin-input"
            placeholder="سجّل التدخل المطلوب أو نتيجة المتابعة"
            required
            maxLength={2000}
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
          <button
            className="admin-btn-primary"
            disabled={saving || !note.trim()}
          >
            {saving ? 'جارٍ الحفظ…' : 'حفظ المتابعة'}
          </button>
        </form>
        <button className="admin-btn-ghost" onClick={() => void loadHistory()}>
          عرض سجل المتابعة
        </button>
        {historyError && <p role="alert">{historyError}</p>}
        {history && (
          <ul className="space-y-3">
            {history.length === 0 && <li>لا توجد متابعة مسجلة.</li>}
            {history.map((entry) => (
              <li key={entry.id}>
                <strong>
                  {statuses[entry.status]} · {entry.actor} · {date(entry.at)}
                </strong>
                <p>{entry.note}</p>
                <p className="text-sm text-[var(--admin-muted)]">
                  السبب وقت التسجيل: {entry.reason}
                </p>
              </li>
            ))}
          </ul>
        )}
      </div>
    </details>
  );
}

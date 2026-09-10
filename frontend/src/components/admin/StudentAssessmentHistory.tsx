'use client';

import { ClipboardCheck, FileCheck2 } from 'lucide-react';
import type { StudentProfileExtendedDto } from '@/services/admin-service';

type AssessmentHistoryItem = {
  id: string;
  title: string;
  packageName?: string | null;
  lessonTitle?: string | null;
  score: number;
  totalScore: number;
  hasFinalGrade: boolean;
  status: string;
  evaluation?: string | null;
  attemptedAt: string;
  passed?: boolean;
  expired?: boolean;
};

const statusLabels: Record<string, string> = {
  InProgress: 'بدأ ولم يسلّم',
  PendingReview: 'بانتظار التصحيح',
  Graded: 'تم التصحيح',
  Missed: 'لم يسلّم',
};

function formatNumber(value: number) {
  return new Intl.NumberFormat('ar-EG-u-nu-latn', { maximumFractionDigits: 2 }).format(value);
}

function formatDate(value: string) {
  return new Date(value).toLocaleString('ar-EG-u-nu-latn', {
    timeZone: 'Africa/Cairo',
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

function StatusBadge({ item }: { item: AssessmentHistoryItem }) {
  const tone = item.status === 'Graded'
    ? item.passed === false ? 'bg-red-500/10 text-red-700' : 'bg-emerald-500/10 text-emerald-700'
    : item.status === 'PendingReview' ? 'bg-amber-500/15 text-amber-800'
      : 'bg-[var(--admin-card-strong)] text-[var(--admin-muted)]';
  return <span className={`inline-flex min-h-7 items-center rounded-full px-2.5 text-xs font-bold ${tone}`}>
    {statusLabels[item.status] ?? item.status}
  </span>;
}

function Grade({ item }: { item: AssessmentHistoryItem }) {
  if (!item.hasFinalGrade) return <span className="text-[var(--admin-muted)]">—</span>;
  return <div className="whitespace-nowrap">
    <span className="text-base font-black text-[var(--admin-text)]">{formatNumber(item.score)}</span>
    <span className="text-[var(--admin-muted)]"> / {formatNumber(item.totalScore)}</span>
    {item.evaluation && <span className="mt-1 block text-xs font-bold text-[var(--admin-primary)]">{item.evaluation}</span>}
  </div>;
}

function HistorySection({ title, description, items, kind }: {
  title: string;
  description: string;
  items: AssessmentHistoryItem[];
  kind: 'exam' | 'homework';
}) {
  const Icon = kind === 'exam' ? FileCheck2 : ClipboardCheck;
  const gradedCount = items.filter(item => item.hasFinalGrade).length;
  return <section className="overflow-hidden rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-bg)] shadow-sm">
    <header className="flex flex-col gap-4 border-b border-[var(--admin-border)] p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
      <div className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
          <Icon size={19} aria-hidden />
        </span>
        <div>
          <h3 className="text-[length:var(--admin-font-title-md)] font-bold text-[var(--admin-text)]">{title}</h3>
          <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">{description}</p>
        </div>
      </div>
      <div className="flex gap-2 text-xs font-bold">
        <span className="rounded-full bg-[var(--admin-card-soft)] px-3 py-1.5 text-[var(--admin-text)]">{items.length} محاولة</span>
        <span className="rounded-full bg-emerald-500/10 px-3 py-1.5 text-emerald-700">{gradedCount} مصححة</span>
      </div>
    </header>

    {items.length === 0 ? <div className="px-6 py-12 text-center text-sm text-[var(--admin-muted)]">
      لا توجد محاولات {kind === 'exam' ? 'امتحانات' : 'واجبات'} لهذا الطالب حتى الآن.
    </div> : <>
      <div className="divide-y divide-[var(--admin-border)] md:hidden">
        {items.map(item => <article key={item.id} className="space-y-3 p-5">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <h4 className="break-words font-bold leading-6 text-[var(--admin-text)]">{item.title}</h4>
              <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">{[item.packageName, item.lessonTitle].filter(Boolean).join(' · ') || 'محتوى مباشر'}</p>
            </div>
            <StatusBadge item={item} />
          </div>
          <div className="flex items-end justify-between gap-4">
            <Grade item={item} />
            <time className="text-left text-xs text-[var(--admin-muted)]" dateTime={item.attemptedAt}>{formatDate(item.attemptedAt)}</time>
          </div>
          {item.expired && <p className="text-xs font-bold text-red-700">انتهى وقت المحاولة</p>}
        </article>)}
      </div>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full min-w-[780px] text-right text-sm">
          <thead className="bg-[var(--admin-card-soft)] text-xs text-[var(--admin-muted)]">
            <tr><th className="px-5 py-3 font-bold">{kind === 'exam' ? 'الامتحان' : 'الواجب'}</th><th className="px-5 py-3 font-bold">الباقة والحصة</th><th className="px-5 py-3 font-bold">الحالة</th><th className="px-5 py-3 font-bold">الدرجة</th><th className="px-5 py-3 font-bold">تاريخ المحاولة</th></tr>
          </thead>
          <tbody className="divide-y divide-[var(--admin-border)]">
            {items.map(item => <tr key={item.id} className="transition-colors hover:bg-[var(--admin-card-soft)]/60">
              <td className="max-w-xs px-5 py-4 font-bold text-[var(--admin-text)]">{item.title}{item.expired && <span className="mt-1 block text-xs text-red-700">انتهى الوقت</span>}</td>
              <td className="max-w-sm px-5 py-4 text-[var(--admin-muted)]"><span className="block text-[var(--admin-text)]">{item.packageName || 'محتوى مباشر'}</span>{item.lessonTitle && <span className="mt-1 block text-xs">{item.lessonTitle}</span>}</td>
              <td className="px-5 py-4"><StatusBadge item={item} /></td>
              <td className="px-5 py-4"><Grade item={item} /></td>
              <td className="whitespace-nowrap px-5 py-4 text-[var(--admin-muted)]"><time dateTime={item.attemptedAt}>{formatDate(item.attemptedAt)}</time></td>
            </tr>)}
          </tbody>
        </table>
      </div>
    </>}
  </section>;
}

export function StudentAssessmentHistory({ examHistory = [], homeworkHistory = [] }: {
  examHistory?: StudentProfileExtendedDto['examHistory'];
  homeworkHistory?: StudentProfileExtendedDto['homeworkHistory'];
}) {
  const exams: AssessmentHistoryItem[] = examHistory.map(item => ({
    id: item.attemptId, ...item, passed: item.hasFinalGrade ? item.isPassed : undefined, expired: item.isTimeExpired,
  }));
  const homeworks: AssessmentHistoryItem[] = homeworkHistory.map(item => ({ id: item.submissionId, ...item }));
  return <div className="space-y-6">
    <HistorySection kind="exam" title="سجل الامتحانات" description="كل محاولات الطالب ودرجاتها وحالة التصحيح في مكان واحد." items={exams} />
    <HistorySection kind="homework" title="سجل الواجبات" description="كل الواجبات التي بدأها الطالب أو سلّمها مع الدرجة النهائية." items={homeworks} />
  </div>;
}

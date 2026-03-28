'use client';

import { ClipboardList, Trash2, Edit2, CheckCircle2, Circle } from 'lucide-react';
import toast from 'react-hot-toast';

interface LessonHomeworkListProps {
  homework: any[];
}

export function LessonHomeworkList({ homework }: LessonHomeworkListProps) {
  if (!homework || homework.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center">
        <div className="mb-4 rounded-full bg-[var(--admin-primary-15)] p-4 text-[var(--admin-primary)]">
          <ClipboardList className="h-8 w-8" />
        </div>
        <h4 className="mb-2 text-lg font-bold text-[var(--admin-text)]">لا توجد واجبات حالياً</h4>
        <p className="max-w-md text-sm text-[var(--admin-muted)] mb-6">
          أضف واجباً لتقييم مستوى الطلاب بهذه الحصة.
        </p>
        <a 
          href="#add-homework-form"
          className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary)] px-5 py-2.5 text-sm font-bold text-white shadow-sm transition hover:opacity-90"
        >
          + أضف واجب جديد
        </a>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {homework.map((hw) => (
        <div
          key={hw.id}
          className="flex items-center justify-between rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4 shadow-sm group"
        >
          <div className="flex items-center gap-4">
            <div className={`rounded-lg p-2.5 border border-[var(--admin-border)] ${hw.isMandatory ? 'bg-amber-500/10 text-amber-500 border-amber-500/20' : 'bg-[var(--admin-card)] text-[var(--admin-text)]'}`}>
              <ClipboardList className="h-4 w-4" />
            </div>
            <div>
              <h4 className="font-bold text-[var(--admin-text)] flex items-center gap-2">
                {hw.title}
                {hw.isMandatory && (
                  <span className="text-[0.65rem] font-bold bg-amber-500/10 text-amber-500 px-1.5 py-0.5 rounded-full border border-amber-500/20">
                    إلزامي
                  </span>
                )}
              </h4>
              <div className="mt-1 flex items-center gap-2 text-xs font-mono text-[var(--admin-muted)]">
                <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                  درجة النجاح: {hw.passingScoreThreshold || 0}
                </span>
                <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                  الأسئلة: {hw.questions?.length || 0}
                </span>
              </div>
            </div>
          </div>
          
          <div className="flex items-center gap-2 opacity-60 group-hover:opacity-100 transition-opacity">
            <div className="relative group/edit">
              <button aria-label="تعديل الواجب" disabled className="rounded-lg p-2 text-[var(--admin-muted)] opacity-40 cursor-not-allowed">
                <Edit2 className="h-4 w-4" />
              </button>
              <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-[10px] font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/edit:opacity-100">قريباً</span>
            </div>
            <div className="relative group/del">
              <button aria-label="حذف الواجب" disabled className="rounded-lg p-2 text-[var(--admin-muted)] opacity-40 cursor-not-allowed">
                <Trash2 className="h-4 w-4" />
              </button>
              <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-[10px] font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/del:opacity-100">قريباً</span>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

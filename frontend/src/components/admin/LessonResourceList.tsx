'use client';

import { FileText, Trash2, Edit2, GripVertical, Download } from 'lucide-react';

interface LessonResourceListProps {
  resources: any[];
}

export function LessonResourceList({ resources }: LessonResourceListProps) {
  if (!resources || resources.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center">
        <div className="mb-4 rounded-full bg-[var(--admin-primary-15)] p-4 text-[var(--admin-primary)]">
          <FileText className="h-8 w-8" />
        </div>
        <h4 className="mb-2 text-lg font-bold text-[var(--admin-text)]">لا توجد ملفات مرفقة</h4>
        <p className="max-w-md text-sm text-[var(--admin-muted)] mb-6">
          أضف مذكرة أو ملف ليتمكن الطلاب من تحميله ومراجعته.
        </p>
        <a 
          href="#add-resource-form"
          className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary)] px-5 py-2.5 text-sm font-bold text-white shadow-sm transition hover:opacity-90"
        >
          + أضف ملف / مذكرة
        </a>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {resources.map((resource) => (
        <div
          key={resource.id}
          className="flex items-center justify-between rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4 shadow-sm group"
        >
          <div className="flex items-center gap-4">
            <div className="flex cursor-grab items-center px-1 text-[var(--admin-muted)] opacity-50 hover:opacity-100">
              <GripVertical className="h-5 w-5" />
            </div>
            <div className="rounded-lg bg-[var(--admin-card)] p-2.5 text-[var(--admin-text)] border border-[var(--admin-border)]">
              <FileText className="h-4 w-4" />
            </div>
            <div>
              <h4 className="font-bold text-[var(--admin-text)]">{resource.title}</h4>
              <div className="mt-1 flex items-center gap-2 text-xs font-mono text-[var(--admin-muted)]">
                <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                  النوع: {resource.resourceType}
                </span>
                <span className="truncate max-w-[200px] text-blue-500 hover:underline">
                  <a href={resource.fileUrl} target="_blank" rel="noreferrer">
                    {resource.fileUrl}
                  </a>
                </span>
              </div>
            </div>
          </div>
          
          <div className="flex items-center gap-2 opacity-60 group-hover:opacity-100 transition-opacity">
            <a 
              href={resource.fileUrl} 
              target="_blank" 
              rel="noreferrer"
              className="rounded-lg p-2 text-[var(--admin-muted)] hover:text-blue-500 hover:bg-blue-500/10 transition-colors"
              title="تحميل الملف"
            >
              <Download className="h-4 w-4" />
            </a>
            <div className="relative group/edit">
              <button aria-label="تعديل الملف" disabled className="rounded-lg p-2 text-[var(--admin-muted)] opacity-40 cursor-not-allowed">
                <Edit2 className="h-4 w-4" />
              </button>
              <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-[10px] font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/edit:opacity-100">قريباً</span>
            </div>
            <div className="relative group/del">
              <button aria-label="حذف الملف" disabled className="rounded-lg p-2 text-[var(--admin-muted)] opacity-40 cursor-not-allowed">
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

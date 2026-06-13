'use client';

import { useCallback, useState, useEffect, forwardRef, useImperativeHandle } from 'react';
import { Folder, Trash2, Edit2, GripVertical, Eye, RefreshCw } from 'lucide-react';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { contentService, ContentSectionDto } from '@/services/content-service';
import toast from 'react-hot-toast';
import Link from 'next/link';
import NeumorphButton from '@/components/ui/neumorph-button';

export interface SectionListManagerRef {
  reload: () => void;
}

interface SectionListManagerProps {
  termId: string;
}

export const SectionListManager = forwardRef<SectionListManagerRef, SectionListManagerProps>(
  ({ termId }, ref) => {
    const [sections, setSections] = useState<ContentSectionDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadError, setLoadError] = useState(false);
    const [confirmTarget, setConfirmTarget] = useState<ContentSectionDto | null>(null);

    const loadSections = useCallback(async () => {
      try {
        setLoading(true);
        setLoadError(false);
        const res = await contentService.getSections(termId);
        const items = (res.data?.data || []) as ContentSectionDto[];
        setSections(items.sort((a, b) => a.order - b.order));
      } catch {
        setLoadError(true);
      } finally {
        setLoading(false);
      }
    }, [termId]);

    useImperativeHandle(ref, () => ({
      reload: loadSections
    }), [loadSections]);

    useEffect(() => {
      if (termId) {
        loadSections();
      }
    }, [termId, loadSections]);

    // Example placeholder, backend doesn't have deleteSection yet but we can prep it or just skip
    function handleDeleteConfirmed() {
      setConfirmTarget(null);
      toast.error('ميزة حذف الأقسام غير متوفرة حالياً.');
    }

    // ── Error / Retry ────────────────────────────────────────────────────
    if (loadError) {
      return (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center gap-4">
          <div className="rounded-full bg-red-100 p-4 text-red-500 dark:bg-red-900/20">
            <RefreshCw className="h-8 w-8" />
          </div>
          <h4 className="text-lg font-bold text-[var(--admin-text)]">تعذّر تحميل الأقسام</h4>
          <p className="max-w-xs text-sm text-[var(--admin-muted)]">تحقق من اتصالك بالإنترنت ثم حاول مجدداً.</p>
          <NeumorphButton onClick={loadSections} intent="ghost" size="md" pill>
            <RefreshCw className="h-4 w-4" /> إعادة المحاولة
          </NeumorphButton>
        </div>
      );
    }

    // ── Loading Skeleton ─────────────────────────────────────────────────
    if (loading) {
      return (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div
              key={i}
              className="flex h-20 w-full animate-pulse items-center justify-between rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 shadow-sm"
              style={{ animationDelay: `${i * 100}ms` }}
            >
              <div className="flex items-center gap-4">
                <div className="h-12 w-12 rounded-xl bg-[var(--admin-muted)] opacity-20" />
                <div className="space-y-2">
                  <div className="h-4 w-32 rounded bg-[var(--admin-muted)] opacity-20" />
                  <div className="h-3 w-20 rounded bg-[var(--admin-muted)] opacity-20" />
                </div>
              </div>
            </div>
          ))}
        </div>
      );
    }

    if (sections.length === 0) {
      return (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center">
          <div className="mb-4 rounded-full bg-[var(--admin-primary-15)] p-4 text-[var(--admin-primary)]">
            <Folder className="h-8 w-8" />
          </div>
          <h4 className="mb-2 text-lg font-bold text-[var(--admin-text)]">لا يوجد قسم بعد</h4>
          <p className="max-w-xs text-sm text-[var(--admin-muted)] mb-6">
            القسم يجمع مجموعة من الحصص تحت عنوان واحد — أضف القسم الأول لهذا الترم.
          </p>
          <NeumorphButton
            asChild
            intent="primary"
            size="lg"
            pill
          >
            <a href="#add-section-form">
              + أضف القسم الأول
            </a>
          </NeumorphButton>
        </div>
      );
    }

    return (
      <>
        <ConfirmDialog
          open={!!confirmTarget}
          title="حذف القسم نهائياً؟"
          description={`سيتم حذف القسم "${confirmTarget?.title}" وجميع الحصص والفيديوهات المرتبطة به بشكل دائم. هذا الإجراء لا يمكن التراجع عنه.`}
          confirmLabel="نعم، احذف القسم"
          cancelLabel="إلغاء"
          variant="danger"
          onConfirm={handleDeleteConfirmed}
          onCancel={() => setConfirmTarget(null)}
        />

        <div className="space-y-4">
          {sections.map((section) => (
            <div
              key={section.id}
              className="flex items-center justify-between rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4 shadow-sm transition-all hover:border-[var(--admin-primary-15)]"
            >
              <div className="flex items-center gap-4">
                <div className="flex cursor-grab items-center px-1 text-[var(--admin-muted)] opacity-50 hover:opacity-100">
                  <GripVertical className="h-5 w-5" />
                </div>
                <div className="rounded-lg bg-[var(--admin-card)] p-2.5 text-[var(--admin-text)] border border-[var(--admin-border)]">
                  <Folder className="h-4 w-4" />
                </div>
                <div>
                  <h4 className="font-bold text-[var(--admin-text)]">{section.title}</h4>
                  <div className="mt-1 flex items-center gap-2 text-xs font-mono text-[var(--admin-muted)]">
                    <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                      ترتيب: {section.order}
                    </span>
                    <span className="rounded bg-[var(--admin-primary-15)] px-1.5 py-0.5 text-[var(--admin-primary)] font-bold">
                      {section.price ? `${section.price} جنيه` : 'مجانى'}
                    </span>
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <NeumorphButton
                  asChild
                  intent="icon"
                  size="icon"
                  title="عرض الحصص وإدارتها"
                >
                  <Link href={`/admin/content/sections/${section.id}`}>
                    <Eye className="h-4 w-4" />
                  </Link>
                </NeumorphButton>

                <div className="relative group/edit">
                  <NeumorphButton
                    disabled
                    intent="icon"
                    size="icon"
                    title="تعديل القسم قريباً"
                  >
                    <Edit2 className="h-4 w-4" />
                  </NeumorphButton>
                  <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-xs font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/edit:opacity-100">
                    قريباً
                  </span>
                </div>

                <div className="relative group/del">
                  <NeumorphButton
                    disabled
                    intent="danger"
                    size="icon"
                    title="حذف القسم قريباً"
                  >
                    <Trash2 className="h-4 w-4" />
                  </NeumorphButton>
                  <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-xs font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/del:opacity-100">
                    قريباً
                  </span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </>
    );
  }
);

SectionListManager.displayName = 'SectionListManager';

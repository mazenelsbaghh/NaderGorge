'use client';

import { useCallback, useState, useEffect, forwardRef, useImperativeHandle } from 'react';
import { Edit3, Eye, Trash2, Calendar, GripVertical, RefreshCw } from 'lucide-react';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { contentService, TermDto } from '@/services/content-service';
import { adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';
import Link from 'next/link';
import NeumorphButton from '@/components/ui/neumorph-button';

export interface TermListManagerRef {
  reload: () => void;
}

interface TermListManagerProps {
  packageId: string;
}

export const TermListManager = forwardRef<TermListManagerRef, TermListManagerProps>(
  ({ packageId }, ref) => {
    const [terms, setTerms] = useState<TermDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadError, setLoadError] = useState(false);
    const [deletingId, setDeletingId] = useState<string | null>(null);
    const [confirmTarget, setConfirmTarget] = useState<TermDto | null>(null);

    const loadTerms = useCallback(async () => {
      try {
        setLoading(true);
        setLoadError(false);
        const res = await contentService.getTerms(packageId);
        const items = (res.data?.data || []) as TermDto[];
        setTerms(items.sort((a, b) => a.order - b.order));
      } catch {
        setLoadError(true);
      } finally {
        setLoading(false);
      }
    }, [packageId]);

    useImperativeHandle(ref, () => ({
      reload: loadTerms
    }), [loadTerms]);

    useEffect(() => {
      if (packageId) {
        loadTerms();
      }
    }, [packageId, loadTerms]);

    async function handleDeleteConfirmed() {
      if (!confirmTarget) return;
      const termId = confirmTarget.id;
      setConfirmTarget(null);
      try {
        setDeletingId(termId);
        await adminService.deleteTerm(termId);
        toast.success('تم حذف الترم وجميع محتوياته بنجاح.');
        loadTerms();
      } catch {
        toast.error('لم نتمكن من حذف الترم. قد يكون مرتبطاً ببيانات أخرى.');
      } finally {
        setDeletingId(null);
      }
    }

    // ── Error / Retry State ────────────────────────────────────────────────
    if (loadError) {
      return (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center gap-4">
          <div className="rounded-full bg-red-100 p-4 text-red-500 dark:bg-red-900/20">
            <RefreshCw className="h-8 w-8" />
          </div>
          <h4 className="text-lg font-bold text-[var(--admin-text)]">تعذّر تحميل الأترام</h4>
          <p className="max-w-xs text-sm text-[var(--admin-muted)]">
            تحقق من اتصالك بالإنترنت ثم حاول مجدداً.
          </p>
          <NeumorphButton onClick={loadTerms} intent="ghost" size="md" pill>
            <RefreshCw className="h-4 w-4" />
            إعادة المحاولة
          </NeumorphButton>
        </div>
      );
    }

    // ── Loading Skeleton ───────────────────────────────────────────────────
    if (loading) {
      return (
        <div className="space-y-3">
          {[1, 2, 3].map(i => (
            <div key={i} className="flex h-20 w-full animate-pulse items-center justify-between rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 shadow-sm" style={{ animationDelay: `${i * 100}ms` }}>
              <div className="flex items-center gap-4">
                <div className="h-12 w-12 rounded-xl bg-[var(--admin-muted)] opacity-20" />
                <div className="space-y-2">
                  <div className="h-4 w-32 rounded bg-[var(--admin-muted)] opacity-20" />
                  <div className="h-3 w-20 rounded bg-[var(--admin-muted)] opacity-20" />
                </div>
              </div>
              <div className="h-8 w-24 rounded-full bg-[var(--admin-muted)] opacity-20" />
            </div>
          ))}
        </div>
      );
    }

    // ── Empty State ────────────────────────────────────────────────────────
    if (terms.length === 0) {
      return (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center">
          <div className="mb-4 rounded-full bg-[var(--admin-primary-15)] p-4 text-[var(--admin-primary)]">
            <Calendar className="h-8 w-8" />
          </div>
          <h4 className="mb-2 text-lg font-bold text-[var(--admin-text)]">لا يوجد ترم بعد</h4>
          <p className="max-w-xs text-sm text-[var(--admin-muted)] mb-6">
            الترم هو الوحدة الكبرى التي تجمع الأقسام والدروس — ابدأ بإضافة الترم الأول.
          </p>
          <NeumorphButton
            asChild
            intent="primary"
            size="lg"
            pill
          >
            <a href="#add-term-form">
              + أضف الترم الأول
            </a>
          </NeumorphButton>
        </div>
      );
    }

    // ── List ───────────────────────────────────────────────────────────────
    return (
      <>
        <ConfirmDialog
          open={!!confirmTarget}
          title="حذف الترم نهائياً؟"
          description={`سيتم حذف الترم "${confirmTarget?.title}" بالإضافة إلى جميع أقسامه ودروسه وفيديوهاته بشكل دائم. هذا الإجراء لا يمكن التراجع عنه.`}
          confirmLabel="نعم، احذف الترم"
          cancelLabel="إلغاء"
          variant="danger"
          onConfirm={handleDeleteConfirmed}
          onCancel={() => setConfirmTarget(null)}
        />

        <div className="space-y-4">
          {terms.map((term) => (
            <div
              key={term.id}
              className="flex items-center justify-between rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4 shadow-sm transition-all hover:border-[var(--admin-primary-15)]"
            >
              <div className="flex items-center gap-4">
                <div className="flex cursor-grab items-center px-1 text-[var(--admin-muted)] opacity-50 hover:opacity-100">
                  <GripVertical className="h-5 w-5" />
                </div>
                <div className="rounded-lg bg-[var(--admin-card)] p-2.5 text-[var(--admin-text)] border border-[var(--admin-border)]">
                  <Calendar className="h-4 w-4" />
                </div>
                <div>
                  <h4 className="font-bold text-[var(--admin-text)]">{term.title}</h4>
                  <div className="mt-1 flex items-center gap-2 text-xs font-mono text-[var(--admin-muted)]">
                    <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                      ترتيب: {term.order}
                    </span>
                    <span className="rounded bg-[var(--admin-primary-15)] px-1.5 py-0.5 text-[var(--admin-primary)] font-bold">
                      {term.price ? `${term.price} جنيه` : 'مجانى'}
                    </span>
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <NeumorphButton
                  asChild
                  intent="icon"
                  size="icon"
                  title="إدارة الأقسام والدروس"
                >
                  <Link href={`/admin/content/terms/${term.id}`}>
                    <Eye className="h-4 w-4" />
                  </Link>
                </NeumorphButton>

                <div className="relative group/edit">
                  <NeumorphButton
                    disabled
                    intent="icon"
                    size="icon"
                    title="تعديل الترم قريباً"
                  >
                    <Edit3 className="h-4 w-4" />
                  </NeumorphButton>
                  <span className="pointer-events-none absolute -top-8 right-0 whitespace-nowrap rounded-lg bg-[var(--admin-text)] px-2 py-1 text-[10px] font-bold text-[var(--admin-bg)] opacity-0 transition-opacity group-hover/edit:opacity-100">
                    قريباً
                  </span>
                </div>

                <NeumorphButton
                  onClick={() => setConfirmTarget(term)}
                  disabled={deletingId === term.id}
                  loading={deletingId === term.id}
                  intent="danger"
                  size="icon"
                  title="حذف الترم وجميع محتوياته"
                >
                  <Trash2 className="h-4 w-4" />
                </NeumorphButton>
              </div>
            </div>
          ))}
        </div>
      </>
    );
  }
);

TermListManager.displayName = 'TermListManager';

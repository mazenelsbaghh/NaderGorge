'use client';

/**
 * ContentHierarchyPanel
 * ─────────────────────
 * Shared component used by Package → Term → Section pages.
 * Shows a list of child items with an inline "add" row at the bottom.
 * No modals, no separate forms — everything is inline.
 */

import { useState, useEffect, useRef, type ReactNode } from 'react';
import Link from 'next/link';
import {
  ChevronLeft,
  GripVertical,
  Plus,
  RefreshCw,
  Trash2,
  Check,
  X,
} from 'lucide-react';
import NeumorphButton from '@/components/ui/neumorph-button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { NumberField } from '@/components/ui/number-field';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface HierarchyItem {
  id: string;
  title: string;
  order: number;
  price?: number;
  /** Optional subtitle (e.g., summary, lesson count) */
  subtitle?: string;
  /** URL to navigate when clicking the item row */
  href?: string;
}

export interface ContentHierarchyPanelProps {
  /** Heading label, e.g. "الأترام" */
  label: string;
  /** Icon node, e.g. <Calendar className="h-5 w-5" /> */
  icon: ReactNode;
  /** Items to display */
  items: HierarchyItem[];
  /** Loading state */
  loading: boolean;
  /** Load error state */
  loadError: boolean;
  /** Empty state description */
  emptyDescription: string;
  /** Placeholder for the title input in the inline add row */
  addPlaceholder: string;
  /** Whether the add row should include a "summary" textarea */
  hasSummary?: boolean;
  /** Called with { title, order, price, summary } to create a new child */
  onCreate: (data: { title: string; order: number; price: number; summary?: string }) => Promise<void>;
  /** Called when deleting an item */
  onDelete?: (id: string) => Promise<void>;
  /** Text for the delete confirm dialog */
  deleteConfirmText?: (item: HierarchyItem) => string;
  /** Retry loading */
  onRetry: () => void;
}

// ─── Skeleton row ─────────────────────────────────────────────────────────────

function SkeletonRow({ delay }: { delay: number }) {
  return (
    <div
      className="flex h-[72px] items-center gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-5 animate-pulse"
      style={{ animationDelay: `${delay}ms` }}
    >
      <div className="h-9 w-9 rounded-xl bg-[var(--admin-muted)] opacity-15" />
      <div className="flex-1 space-y-2">
        <div className="h-3.5 w-40 rounded bg-[var(--admin-muted)] opacity-15" />
        <div className="h-2.5 w-24 rounded bg-[var(--admin-muted)] opacity-10" />
      </div>
    </div>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────

export function ContentHierarchyPanel({
  label,
  icon,
  items,
  loading,
  loadError,
  emptyDescription,
  addPlaceholder,
  hasSummary = false,
  onCreate,
  onDelete,
  deleteConfirmText,
  onRetry,
}: ContentHierarchyPanelProps) {
  const [isAdding, setIsAdding] = useState(false);
  const [newTitle, setNewTitle] = useState('');
  const [newSummary, setNewSummary] = useState('');
  const [newOrder, setNewOrder] = useState(1);
  const [newPrice, setNewPrice] = useState(0);
  const [saving, setSaving] = useState(false);
  const [confirmTarget, setConfirmTarget] = useState<HierarchyItem | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const titleInputRef = useRef<HTMLInputElement>(null);

  // Auto-set order to next available
  useEffect(() => {
    if (isAdding) {
      const nextOrder = items.length > 0 ? Math.max(...items.map((i) => i.order)) + 1 : 1;
      setNewOrder(nextOrder);
      setTimeout(() => titleInputRef.current?.focus(), 60);
    }
  }, [isAdding, items]);

  async function handleCreate() {
    if (!newTitle.trim()) return;
    if (hasSummary && !newSummary.trim()) return;
    try {
      setSaving(true);
      await onCreate({ title: newTitle.trim(), order: newOrder, price: newPrice, summary: newSummary.trim() || undefined });
      setNewTitle('');
      setNewSummary('');
      setNewPrice(0);
      setIsAdding(false);
    } finally {
      setSaving(false);
    }
  }

  async function handleDeleteConfirmed() {
    if (!confirmTarget || !onDelete) return;
    const id = confirmTarget.id;
    setConfirmTarget(null);
    try {
      setDeletingId(id);
      await onDelete(id);
    } finally {
      setDeletingId(null);
    }
  }

  // ── Error state ──────────────────────────────────────────────────────────────
  if (loadError) {
    return (
      <div className="flex flex-col items-center justify-center rounded-3xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] py-16 text-center gap-4">
        <div className="rounded-full bg-red-100 p-4 text-red-500 dark:bg-red-950/30">
          <RefreshCw className="h-7 w-7" />
        </div>
        <p className="text-sm font-bold text-[var(--admin-text)]">تعذّر تحميل البيانات</p>
        <NeumorphButton onClick={onRetry} intent="ghost" size="md" pill>
          <RefreshCw className="h-4 w-4" />
          إعادة المحاولة
        </NeumorphButton>
      </div>
    );
  }

  // ── Loading state ────────────────────────────────────────────────────────────
  if (loading) {
    return (
      <div className="space-y-3">
        {[0, 100, 200].map((d) => <SkeletonRow key={d} delay={d} />)}
      </div>
    );
  }

  return (
    <>
      <ConfirmDialog
        open={!!confirmTarget}
        variant="danger"
        title={confirmTarget ? `حذف "${confirmTarget.title}"؟` : ''}
        description={confirmTarget ? (deleteConfirmText?.(confirmTarget) ?? `سيتم حذف "${confirmTarget.title}" وجميع محتوياته نهائياً. هذا الإجراء لا يمكن التراجع عنه.`) : ''}
        confirmLabel="نعم، احذف"
        cancelLabel="إلغاء"
        onConfirm={handleDeleteConfirmed}
        onCancel={() => setConfirmTarget(null)}
      />

      <div className="space-y-2">
        {/* Header row */}
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2 text-[var(--admin-text)]">
            <span className="text-[var(--admin-primary)]">{icon}</span>
            <h3 className="text-lg font-black">{label}</h3>
            {items.length > 0 && (
              <span className="rounded-full bg-[var(--admin-primary-15)] px-2.5 py-0.5 text-xs font-black text-[var(--admin-primary)]">
                {items.length}
              </span>
            )}
          </div>

          {!isAdding && (
            <NeumorphButton
              onClick={() => setIsAdding(true)}
              intent="primary"
              size="sm"
              pill
            >
              <Plus className="h-3.5 w-3.5" />
              إضافة
            </NeumorphButton>
          )}
        </div>

        {/* Empty state */}
        {items.length === 0 && !isAdding && (
          <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-strong)]/40 py-14 text-center">
            <div className="mb-4 rounded-full bg-[var(--admin-primary-15)] p-4 text-[var(--admin-primary)]">
              {icon}
            </div>
            <p className="mb-1 font-bold text-[var(--admin-text)]">لا يوجد {label} بعد</p>
            <p className="mb-6 max-w-xs text-sm text-[var(--admin-muted)]">{emptyDescription}</p>
            <NeumorphButton onClick={() => setIsAdding(true)} intent="primary" size="md" pill>
              <Plus className="h-4 w-4" />
              إضافة أول {label.replace('ال', '')}
            </NeumorphButton>
          </div>
        )}

        {/* Item list */}
        {items.map((item) => {
          const isDeleting = deletingId === item.id;
          const Row = (
            <div
              key={item.id}
              className={`group flex items-center gap-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-4 py-3.5 shadow-sm transition-all ${
                item.href
                  ? 'cursor-pointer hover:border-[var(--admin-primary)] hover:shadow-[0_0_0_1px_var(--admin-primary)] hover:bg-[var(--admin-card)]'
                  : ''
              } ${isDeleting ? 'opacity-40' : ''}`}
            >
              {/* Drag handle */}
              <div className="text-[var(--admin-muted)] opacity-30 group-hover:opacity-70 transition-opacity cursor-grab shrink-0">
                <GripVertical className="h-5 w-5" />
              </div>

              {/* Order badge */}
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-[var(--admin-primary-15)] text-xs font-black text-[var(--admin-primary)]">
                {item.order}
              </span>

              {/* Content */}
              <div className="flex-1 min-w-0">
                <p className="font-bold text-[var(--admin-text)] leading-tight truncate">{item.title}</p>
                {item.subtitle && (
                  <p className="text-xs text-[var(--admin-muted)] mt-0.5 truncate">{item.subtitle}</p>
                )}
              </div>

              {/* Price badge */}
              {item.price !== undefined && (
                <span className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-bold ${
                  item.price > 0
                    ? 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]'
                    : 'bg-[var(--admin-card)] border border-[var(--admin-border)] text-[var(--admin-muted)]'
                }`}>
                  {item.price > 0 ? `${item.price} ج` : 'مجاني'}
                </span>
              )}

              {/* Actions */}
              {item.href && (
                <ChevronLeft className="h-4 w-4 text-[var(--admin-muted)] opacity-0 group-hover:opacity-100 transition-opacity shrink-0" />
              )}

              {onDelete && (
                <button
                  type="button"
                  onClick={(e) => { e.preventDefault(); e.stopPropagation(); if (!isDeleting) setConfirmTarget(item); }}
                  disabled={isDeleting}
                  className="shrink-0 rounded-xl p-2 text-[var(--admin-muted)] opacity-0 group-hover:opacity-100 transition-all hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40"
                  title="حذف"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              )}
            </div>
          );

          return item.href ? (
            <Link key={item.id} href={item.href} className="block">
              {Row}
            </Link>
          ) : (
            <div key={item.id}>{Row}</div>
          );
        })}

        {/* Inline add row */}
        {isAdding && (
          <div className="rounded-2xl border-2 border-dashed border-[var(--admin-primary)] bg-[var(--admin-primary-15)]/30 p-4 space-y-3">
            <div className="flex items-start gap-3">
              <div className="flex-1 space-y-2">
                <input
                  ref={titleInputRef}
                  type="text"
                  value={newTitle}
                  onChange={(e) => setNewTitle(e.target.value)}
                  placeholder={addPlaceholder}
                  className="admin-input"
                  onKeyDown={(e) => { if (e.key === 'Enter' && !hasSummary) { e.preventDefault(); void handleCreate(); } if (e.key === 'Escape') setIsAdding(false); }}
                />
                {hasSummary && (
                  <textarea
                    value={newSummary}
                    onChange={(e) => setNewSummary(e.target.value)}
                    placeholder="نبذة قصيرة عن محتوى الحصة..."
                    rows={2}
                    className="admin-input resize-none"
                  />
                )}
              </div>

              <div className="w-24 shrink-0">
                <NumberField value={newOrder} onChange={setNewOrder} minValue={1}>
                  <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] block mb-1.5">ترتيب</NumberField.Label>
                  <NumberField.Group className="h-11 w-full">
                    <NumberField.DecrementButton />
                    <NumberField.Input />
                    <NumberField.IncrementButton />
                  </NumberField.Group>
                </NumberField>
              </div>

              <div className="w-28 shrink-0">
                <NumberField value={newPrice} onChange={setNewPrice} minValue={0}>
                  <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] block mb-1.5">السعر (ج)</NumberField.Label>
                  <NumberField.Group className="h-11 w-full">
                    <NumberField.DecrementButton />
                    <NumberField.Input />
                    <NumberField.IncrementButton />
                  </NumberField.Group>
                </NumberField>
              </div>
            </div>

            <div className="flex items-center justify-end gap-2">
              <button
                type="button"
                onClick={() => { setIsAdding(false); setNewTitle(''); setNewSummary(''); }}
                className="flex items-center gap-1.5 rounded-xl border border-[var(--admin-border)] px-4 py-2 text-sm font-bold text-[var(--admin-muted)] transition hover:bg-[var(--admin-card-strong)]"
              >
                <X className="h-3.5 w-3.5" />
                إلغاء
              </button>
              <NeumorphButton
                onClick={() => void handleCreate()}
                disabled={saving || !newTitle.trim() || (hasSummary && !newSummary.trim())}
                loading={saving}
                intent="primary"
                size="md"
                pill
              >
                <Check className="h-3.5 w-3.5" />
                حفظ
              </NeumorphButton>
            </div>
          </div>
        )}
      </div>
    </>
  );
}

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
import Image from 'next/image';
import {
  ChevronLeft,
  GripVertical,
  Plus,
  RefreshCw,
  Trash2,
  Check,
  X,
  Camera,
  Loader2,
  Image as ImageIcon,
  Pencil,
} from 'lucide-react';
import NeumorphButton from '@/components/ui/neumorph-button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { NumberField } from '@/components/ui/number-field';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import toast from 'react-hot-toast';
import { ContentArchiveControl } from './ContentArchiveControl';
import type { ContentArchiveMode, ContentArchiveTargetType } from '@/services/admin-service';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface HierarchyItem {
  id: string;
  title: string;
  order: number;
  price?: number;
  imageUrl?: string | null;
  /** Optional subtitle (e.g., summary, lesson count) */
  subtitle?: string;
  /** URL to navigate when clicking the item row */
  href?: string;
  archiveMode?: ContentArchiveMode;
  archivedAt?: string | null;
  archiveTargetType?: ContentArchiveTargetType;
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
  /** Whether the panel supports uploading/displaying images */
  hasImage?: boolean;
  /** Whether new child items can be added from this panel */
  canCreate?: boolean;
  /** Called with { title, order, price, summary, imageFile } to create a new child */
  onCreate: (data: { title: string; order: number; price: number; summary?: string; imageFile?: File | null }) => Promise<void>;
  /** Optional inline update for existing rows */
  onUpdate?: (id: string, data: { title: string; order: number; price: number; summary?: string }) => Promise<void>;
  /** Optional callback to upload an image for an existing item */
  onImageUpload?: (id: string, file: File) => Promise<void>;
  /** Called when deleting an item */
  onDelete?: (id: string) => Promise<void>;
  /** Text for the delete confirm dialog */
  deleteConfirmText?: (item: HierarchyItem) => string;
  /** Retry loading */
  onRetry: () => void;
  /** Reload after an item is archived or restored. */
  onArchiveChanged?: () => void | Promise<void>;
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
  hasImage = false,
  canCreate = true,
  onCreate,
  onUpdate,
  onImageUpload,
  onDelete,
  deleteConfirmText,
  onRetry,
  onArchiveChanged,
}: ContentHierarchyPanelProps) {
  const [isAdding, setIsAdding] = useState(false);
  const [newTitle, setNewTitle] = useState('');
  const [newSummary, setNewSummary] = useState('');
  const [newOrder, setNewOrder] = useState(1);
  const [newPrice, setNewPrice] = useState(0);
  const [newImageFile, setNewImageFile] = useState<File | null>(null);
  const [newImagePreview, setNewImagePreview] = useState<string | null>(null);
  const [rowUploadingId, setRowUploadingId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [editingItem, setEditingItem] = useState<HierarchyItem | null>(null);
  const [editTitle, setEditTitle] = useState('');
  const [editSummary, setEditSummary] = useState('');
  const [editOrder, setEditOrder] = useState(1);
  const [editPrice, setEditPrice] = useState(0);
  const [updating, setUpdating] = useState(false);
  const [confirmTarget, setConfirmTarget] = useState<HierarchyItem | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [contentView, setContentView] = useState<'current' | 'archived'>('current');
  const titleInputRef = useRef<HTMLInputElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const currentItems = items.filter((item) => (item.archiveMode ?? 'None') === 'None');
  const archivedItems = items.filter((item) => (item.archiveMode ?? 'None') !== 'None');
  const visibleItems = contentView === 'current' ? currentItems : archivedItems;

  // Auto-set order to next available
  useEffect(() => {
    if (isAdding) {
      const nextOrder = items.length > 0 ? Math.max(...items.map((i) => i.order)) + 1 : 1;
      setNewOrder(nextOrder);
      setTimeout(() => titleInputRef.current?.focus(), 60);
    }
  }, [isAdding, items]);

  async function handleRowImageChange(id: string, file?: File) {
    if (!file || !onImageUpload) return;
    if (!file.type.startsWith('image/')) {
      toast.error('اختر ملف صورة صالحًا.');
      return;
    }
    try {
      setRowUploadingId(id);
      await onImageUpload(id, file);
      toast.success('تم تحديث الصورة بنجاح.');
    } catch {
      toast.error('تعذر رفع الصورة.');
    } finally {
      setRowUploadingId(null);
    }
  }

  async function handleCreate() {
    if (!newTitle.trim()) return;
    if (hasSummary && !newSummary.trim()) return;
    try {
      setSaving(true);
      await onCreate({
        title: newTitle.trim(),
        order: newOrder,
        price: newPrice,
        summary: newSummary.trim() || undefined,
        imageFile: newImageFile,
      });
      setNewTitle('');
      setNewSummary('');
      setNewPrice(0);
      setNewImageFile(null);
      setNewImagePreview(null);
      setIsAdding(false);
    } finally {
      setSaving(false);
    }
  }

  function startEditing(item: HierarchyItem) {
    setEditingItem(item);
    setEditTitle(item.title);
    setEditSummary(item.subtitle ?? '');
    setEditOrder(item.order);
    setEditPrice(item.price ?? 0);
  }

  function cancelEditing() {
    setEditingItem(null);
    setEditTitle('');
    setEditSummary('');
    setEditOrder(1);
    setEditPrice(0);
  }

  async function handleUpdate() {
    if (!editingItem || !onUpdate || !editTitle.trim()) return;
    if (hasSummary && !editSummary.trim()) return;
    try {
      setUpdating(true);
      await onUpdate(editingItem.id, {
        title: editTitle.trim(),
        order: editOrder,
        price: editPrice,
        summary: editSummary.trim() || undefined,
      });
      cancelEditing();
    } finally {
      setUpdating(false);
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

          {canCreate && !isAdding && (
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

        <div className="mb-4 grid grid-cols-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-1" role="tablist" aria-label="حالة المحتوى">
          <button
            type="button"
            role="tab"
            aria-selected={contentView === 'current'}
            onClick={() => setContentView('current')}
            className={`min-h-11 rounded-lg px-3 text-sm font-black transition ${contentView === 'current' ? 'bg-[var(--admin-primary)] text-white' : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'}`}
          >
            المحتوى الحالي ({currentItems.length})
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={contentView === 'archived'}
            onClick={() => setContentView('archived')}
            className={`min-h-11 rounded-lg px-3 text-sm font-black transition ${contentView === 'archived' ? 'bg-amber-700 text-white' : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'}`}
          >
            المؤرشف ({archivedItems.length})
          </button>
        </div>

        {/* Empty state */}
        {visibleItems.length === 0 && !isAdding && (
          <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-strong)]/40 py-14 text-center">
            <div className="mb-4 rounded-full bg-[var(--admin-primary-15)] p-4 text-[var(--admin-primary)]">
              {icon}
            </div>
            <p className="mb-1 font-bold text-[var(--admin-text)]">{contentView === 'archived' ? `لا يوجد ${label} مؤرشف` : `لا يوجد ${label} بعد`}</p>
            <p className="mb-6 max-w-xs text-sm text-[var(--admin-muted)]">{contentView === 'archived' ? 'عند أرشفة أي عنصر سيظهر هنا مع إمكانية إعادته للمحتوى الحالي.' : emptyDescription}</p>
            {canCreate && contentView === 'current' && (
              <NeumorphButton onClick={() => setIsAdding(true)} intent="primary" size="md" pill>
                <Plus className="h-4 w-4" />
                إضافة أول {label.replace('ال', '')}
              </NeumorphButton>
            )}
          </div>
        )}

        {/* Item list */}
        {visibleItems.map((item) => {
          const isDeleting = deletingId === item.id;
          const isEditing = editingItem?.id === item.id;

          if (isEditing) {
            return (
              <div key={item.id} className="rounded-2xl border-2 border-[var(--admin-primary)] bg-[var(--admin-primary-15)]/25 p-4">
                <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_112px_128px_auto] lg:items-end">
                  <label className="space-y-1.5">
                    <span className="text-xs font-bold text-[var(--admin-muted)]">الاسم</span>
                    <input
                      autoFocus
                      type="text"
                      value={editTitle}
                      onChange={(e) => setEditTitle(e.target.value)}
                      className="admin-input"
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' && !hasSummary) {
                          e.preventDefault();
                          void handleUpdate();
                        }
                        if (e.key === 'Escape') cancelEditing();
                      }}
                    />
                  </label>

                  <NumberField value={editOrder} onChange={setEditOrder} minValue={1}>
                    <NumberField.Label className="mb-1.5 block text-xs font-bold text-[var(--admin-muted)]">الترتيب</NumberField.Label>
                    <NumberField.Group className="h-11">
                      <NumberField.DecrementButton />
                      <NumberField.Input />
                      <NumberField.IncrementButton />
                    </NumberField.Group>
                  </NumberField>

                  <NumberField value={editPrice} onChange={setEditPrice} minValue={0}>
                    <NumberField.Label className="mb-1.5 block text-xs font-bold text-[var(--admin-muted)]">السعر (ج)</NumberField.Label>
                    <NumberField.Group className="h-11">
                      <NumberField.DecrementButton />
                      <NumberField.Input />
                      <NumberField.IncrementButton />
                    </NumberField.Group>
                  </NumberField>

                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={cancelEditing}
                      disabled={updating}
                      className="inline-flex h-11 items-center gap-1.5 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-muted)] transition hover:bg-[var(--admin-card-strong)] disabled:opacity-50"
                    >
                      <X className="h-3.5 w-3.5" />
                      إلغاء
                    </button>
                    <NeumorphButton
                      onClick={() => void handleUpdate()}
                      disabled={updating || !editTitle.trim() || (hasSummary && !editSummary.trim())}
                      loading={updating}
                      intent="primary"
                      size="md"
                      pill
                    >
                      <Check className="h-3.5 w-3.5" />
                      حفظ
                    </NeumorphButton>
                  </div>
                </div>

                {hasSummary && (
                  <label className="mt-3 block space-y-1.5">
                    <span className="text-xs font-bold text-[var(--admin-muted)]">نبذة الحصة</span>
                    <textarea
                      value={editSummary}
                      onChange={(e) => setEditSummary(e.target.value)}
                      rows={2}
                      className="admin-input resize-none"
                    />
                  </label>
                )}
              </div>
            );
          }

          const Row = (
            <div
              key={item.id}
              className={`group flex items-center gap-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-4 py-3.5 shadow-sm transition-[color,background-color,border-color,opacity,transform,box-shadow] ${
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

              {/* Image Thumbnail */}
              {hasImage && (
                <div className="relative h-11 w-[78px] rounded-lg overflow-hidden bg-[var(--admin-card-strong)] border border-[var(--admin-border)] shrink-0 group/img cursor-pointer">
                  {item.imageUrl ? (
                    <Image
                      src={resolveMediaUrl(item.imageUrl)}
                      alt={item.title}
                      fill
                      className="object-cover"
                      sizes="78px"
                    />
                  ) : (
                    <div className="flex h-full w-full items-center justify-center bg-[var(--admin-muted)]/10 text-[var(--admin-muted)]">
                      <ImageIcon className="h-4 w-4" />
                    </div>
                  )}
                  
                  {onImageUpload && (
                    <>
                      <input
                        type="file"
                        accept="image/*"
                        className="hidden"
                        id={`row-file-input-${item.id}`}
                        onChange={(e) => void handleRowImageChange(item.id, e.target.files?.[0])}
                        onClick={(e) => e.stopPropagation()}
                      />
                      <button
                        type="button"
                        disabled={rowUploadingId === item.id}
                        onClick={(e) => {
                          e.preventDefault();
                          e.stopPropagation();
                          document.getElementById(`row-file-input-${item.id}`)?.click();
                        }}
                        className="absolute inset-0 bg-black/60 flex items-center justify-center text-white opacity-0 group-hover/img:opacity-100 transition-opacity"
                      >
                        {rowUploadingId === item.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Camera className="h-4 w-4" />
                        )}
                      </button>
                    </>
                  )}
                </div>
              )}

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
              {item.archiveTargetType && (
                <ContentArchiveControl
                  targetType={item.archiveTargetType}
                  targetId={item.id}
                  title={item.title}
                  archiveMode={item.archiveMode}
                  onChanged={onArchiveChanged}
                  compact
                />
              )}

              {item.href && (
                <ChevronLeft className="h-4 w-4 text-[var(--admin-muted)] opacity-0 group-hover:opacity-100 transition-opacity shrink-0" />
              )}

              {onUpdate && (
                <button
                  type="button"
                  onClick={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    startEditing(item);
                  }}
                  disabled={isDeleting}
                  className="shrink-0 rounded-xl p-2 text-[var(--admin-muted)] opacity-0 transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] group-hover:opacity-100 disabled:opacity-40"
                  title="تعديل"
                  aria-label={`تعديل ${item.title}`}
                >
                  <Pencil className="h-4 w-4" />
                </button>
              )}

              {onDelete && (
                <button
                  type="button"
                  onClick={(e) => { e.preventDefault(); e.stopPropagation(); if (!isDeleting) setConfirmTarget(item); }}
                  disabled={isDeleting}
                  className="shrink-0 rounded-xl p-2 text-[var(--admin-muted)] opacity-0 group-hover:opacity-100 transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40"
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
        {isAdding && contentView === 'current' && (
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

              <div className="w-28 shrink-0">
                <NumberField value={newOrder} onChange={setNewOrder} minValue={1}>
                  <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] block mb-1.5">ترتيب</NumberField.Label>
                  <NumberField.Group className="h-11 w-full">
                    <NumberField.DecrementButton />
                    <NumberField.Input />
                    <NumberField.IncrementButton />
                  </NumberField.Group>
                </NumberField>
              </div>

              <div className="w-32 shrink-0">
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

            {hasImage && (
              <div className="flex items-center gap-3 border-t border-[var(--admin-border)]/50 pt-3">
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (file) {
                      setNewImageFile(file);
                      setNewImagePreview(URL.createObjectURL(file));
                    }
                  }}
                />
                <div 
                  onClick={() => fileInputRef.current?.click()}
                  className="relative h-14 w-24 border border-dashed border-[var(--admin-border)] rounded-xl flex items-center justify-center bg-[var(--admin-card-soft)] cursor-pointer hover:border-[var(--admin-primary)] overflow-hidden shrink-0"
                >
                  {newImagePreview ? (
                    <>
                      {/* eslint-disable-next-line @next/next/no-img-element */}
                      <img src={newImagePreview} alt="Preview" className="w-full h-full object-cover" />
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          setNewImageFile(null);
                          setNewImagePreview(null);
                          if (fileInputRef.current) fileInputRef.current.value = '';
                        }}
                        className="absolute inset-0 bg-black/40 flex items-center justify-center text-white opacity-0 hover:opacity-100 transition-opacity"
                      >
                        <X className="h-4 w-4" />
                      </button>
                    </>
                  ) : (
                    <div className="flex flex-col items-center justify-center gap-1 text-[var(--admin-muted)] text-xs font-bold">
                      <Camera className="h-4 w-4 text-[var(--admin-primary)]" />
                      <span>صورة الغلاف</span>
                    </div>
                  )}
                </div>
                <div className="text-xs text-[var(--admin-muted)]">
                  اضغط لرفع صورة غلاف مخصصة (اختياري). يتم تحويلها تلقائيًا إلى WebP.
                </div>
              </div>
            )}

            <div className="flex items-center justify-end gap-2">
              <button
                type="button"
                onClick={() => { setIsAdding(false); setNewTitle(''); setNewSummary(''); setNewImageFile(null); setNewImagePreview(null); }}
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

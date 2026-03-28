'use client';

import { useEffect, useRef } from 'react';
import { AlertTriangle, X } from 'lucide-react';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'danger' | 'warning' | 'primary';
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel = 'تأكيد',
  cancelLabel = 'إلغاء',
  variant = 'danger',
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const cancelBtnRef = useRef<HTMLButtonElement>(null);

  // Focus trap & keyboard
  useEffect(() => {
    if (!open) return;
    cancelBtnRef.current?.focus();

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onCancel();
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [open, onCancel]);

  if (!open) return null;

  const isD = variant === 'danger';
  const isP = variant === 'primary';
  const isW = variant === 'warning';

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-dialog-title"
      aria-describedby="confirm-dialog-desc"
      className="fixed inset-0 z-[100] flex items-center justify-center p-4 animate-in fade-in duration-200"
      dir="rtl"
    >
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/60 backdrop-blur-[2px]"
        onClick={onCancel}
        aria-hidden="true"
      />

      {/* Dialog Card */}
      <div className="relative z-10 w-full max-w-md rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-2xl animate-in zoom-in-95 duration-200">
        {/* Header */}
        <div className="flex items-start justify-between gap-4 p-6 pb-4">
          <div className="flex items-center gap-3">
            <div
              className={`rounded-full p-2.5 ${
                isD
                  ? 'bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-400'
                  : isP
                  ? 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]'
                  : 'bg-amber-100 text-amber-600 dark:bg-amber-900/30 dark:text-amber-400'
              }`}
            >
              <AlertTriangle className="h-5 w-5" />
            </div>
            <h2
              id="confirm-dialog-title"
              className="text-lg font-extrabold text-[var(--admin-text)]"
            >
              {title}
            </h2>
          </div>
          <button
            onClick={onCancel}
            aria-label="إغلاق"
            className="rounded-xl p-2 text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Body */}
        <p
          id="confirm-dialog-desc"
          className="px-6 pb-6 text-sm leading-relaxed text-[var(--admin-muted)]"
        >
          {description}
        </p>

        {/* Actions */}
        <div className="flex flex-col-reverse gap-3 border-t border-[var(--admin-border)] px-6 py-4 sm:flex-row sm:justify-end">
          <button
            ref={cancelBtnRef}
            onClick={onCancel}
            className="h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-6 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
          >
            {cancelLabel}
          </button>
          <button
            onClick={onConfirm}
            className={`h-11 rounded-xl px-6 text-sm font-bold text-white transition-all active:scale-95 ${
              isD
                ? 'bg-red-600 hover:bg-red-700 active:bg-red-800'
                : isP
                ? 'bg-[var(--admin-primary)] hover:brightness-110'
                : 'bg-amber-600 hover:bg-amber-700 active:bg-amber-800'
            }`}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

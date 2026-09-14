'use client';

import { useId, useRef } from 'react';
import { AlertTriangle, X } from 'lucide-react';
import { AccessibleOverlay } from '@/components/ui/AccessibleOverlay';

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
  const titleId = useId();
  const descriptionId = useId();

  const isD = variant === 'danger';
  const isP = variant === 'primary';

  return (
    <AccessibleOverlay
      open={open}
      onClose={onCancel}
      labelledBy={titleId}
      describedBy={descriptionId}
      initialFocusRef={cancelBtnRef}
      layerClassName="flex items-center justify-center p-4"
      backdropClassName="bg-black/60 backdrop-blur-[2px]"
      className="relative w-full max-w-[440px] rounded-2xl bg-[var(--admin-bg)] p-6 shadow-xl animate-in zoom-in-95 duration-200"
    >
      <div dir="rtl">
        <button
          type="button"
          onClick={onCancel}
          aria-label="إغلاق"
          className="absolute start-5 top-5 inline-flex size-11 items-center justify-center rounded-full text-[var(--admin-muted)] transition-colors hover:bg-[var(--admin-hover)]"
        >
          <X className="h-4 w-4" />
        </button>

        <div className="flex flex-col items-center gap-3 pb-4">
          <div
            className={`rounded-full p-2.5 flex items-center justify-center ${
              isD
                ? 'bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-400'
                : isP
                ? 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]'
                : 'bg-amber-100 text-amber-600 dark:bg-amber-900/30 dark:text-amber-400'
            }`}
          >
            <AlertTriangle className="h-6 w-6" />
          </div>
          <h2
            id={titleId}
            className="text-xl font-black text-[var(--admin-text)] text-center mt-1"
          >
            {title}
          </h2>
        </div>

        <p
          id={descriptionId}
          className="px-6 pb-6 text-sm leading-relaxed text-[var(--admin-muted)] text-center font-medium"
        >
          {description}
        </p>

        <div className="flex flex-col-reverse items-stretch justify-center gap-3 sm:flex-row sm:items-center">
          <button
            type="button"
            ref={cancelBtnRef}
            onClick={onCancel}
            className="h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-8 text-sm font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-hover)]"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className={`h-11 rounded-xl px-8 text-sm font-bold text-white transition-[color,background-color,transform] active:translate-y-px ${
              isD
                ? 'bg-red-600 hover:bg-red-700'
                : isP
                ? 'bg-[var(--admin-primary-strong)] hover:brightness-110'
                : 'bg-amber-600 hover:bg-amber-700'
            }`}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </AccessibleOverlay>
  );
}

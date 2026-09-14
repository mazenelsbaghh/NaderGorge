'use client';

import { useEffect, useId, useState } from 'react';
import { AdminModal } from './AdminModal';

export type AdminConfirmationDialogVariant = 'primary' | 'danger';

export interface AdminConfirmationDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => void | Promise<void>;
  title: string;
  consequence: string;
  confirmLabel: string;
  cancelLabel?: string;
  variant?: AdminConfirmationDialogVariant;
  reasonLabel?: string;
  reasonPlaceholder?: string;
  reasonRequired?: boolean;
  isConfirming?: boolean;
}

/**
 * A consistent, accessible confirmation step for irreversible or high-impact
 * admin actions. It deliberately requires the caller to own the async state so
 * API errors can leave the dialog open and let the administrator try again.
 */
export function AdminConfirmationDialog({
  open,
  onClose,
  onConfirm,
  title,
  consequence,
  confirmLabel,
  cancelLabel = 'إلغاء',
  variant = 'primary',
  reasonLabel,
  reasonPlaceholder = 'اكتب سبب الإجراء للتوثيق',
  reasonRequired = false,
  isConfirming = false,
}: AdminConfirmationDialogProps) {
  const [reason, setReason] = useState('');
  const reasonId = useId();
  const consequenceId = useId();
  const requiresReason = reasonRequired && !reason.trim();
  const isDanger = variant === 'danger';

  useEffect(() => {
    if (!open) setReason('');
  }, [open]);

  const handleClose = () => {
    if (!isConfirming) onClose();
  };

  const handleConfirm = () => {
    if (requiresReason || isConfirming) return;
    void onConfirm(reason.trim());
  };

  const confirmClassName = isDanger
    ? 'bg-[var(--admin-danger)] text-white hover:brightness-110 focus-visible:ring-[var(--admin-danger)]'
    : 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] hover:bg-[var(--admin-primary-strong)] focus-visible:ring-[var(--admin-primary)]';

  return (
    <AdminModal open={open} onClose={handleClose} title={title} maxWidth="max-w-lg">
      <div className="space-y-5">
        <div
          id={consequenceId}
          className={`rounded-xl border p-4 text-sm font-semibold leading-6 ${
            isDanger
              ? 'border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'
              : 'border-[var(--admin-primary-15)] bg-[var(--admin-primary-15)] text-[var(--admin-text)]'
          }`}
        >
          {consequence}
        </div>

        {(reasonRequired || reasonLabel) && (
          <div>
            <label htmlFor={reasonId} className="mb-1.5 block text-sm font-bold text-[var(--admin-text)]">
              {reasonLabel}{reasonRequired ? <span aria-hidden="true"> *</span> : null}
            </label>
            <textarea
              id={reasonId}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder={reasonPlaceholder}
              required={reasonRequired}
              rows={3}
              disabled={isConfirming}
              aria-required={reasonRequired || undefined}
              className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus-visible:border-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary-15)] disabled:cursor-not-allowed disabled:opacity-60"
            />
          </div>
        )}

        <div className="flex flex-wrap justify-end gap-2 border-t border-[var(--admin-border)] pt-4">
          <button
            type="button"
            onClick={handleClose}
            disabled={isConfirming}
            className="min-h-10 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 text-sm font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:cursor-not-allowed disabled:opacity-60"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            disabled={requiresReason || isConfirming}
            aria-describedby={consequenceId}
            className={`min-h-10 rounded-xl px-5 text-sm font-black transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)] disabled:cursor-not-allowed disabled:opacity-60 ${confirmClassName}`}
          >
            {isConfirming ? 'جارٍ التنفيذ...' : confirmLabel}
          </button>
        </div>
      </div>
    </AdminModal>
  );
}

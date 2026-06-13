'use client';

import { useEffect, useRef, type ReactNode } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X } from 'lucide-react';
import NeumorphButton from '@/components/ui/neumorph-button';

/* ── Size map ──────────────────────────────────────────────────────── */

const sizeMap = {
  sm: 'max-w-sm',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
} as const;

/* ── AdminModal ────────────────────────────────────────────────────── */

interface AdminModalProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  children: ReactNode;
  size?: 'sm' | 'md' | 'lg';
}

export function AdminModal({
  open,
  onClose,
  title,
  children,
  size = 'md',
}: AdminModalProps) {
  const cardRef = useRef<HTMLDivElement>(null);

  /* Escape key */
  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [open, onClose]);

  /* Focus first focusable element inside the card */
  useEffect(() => {
    if (!open) return;

    requestAnimationFrame(() => {
      const firstFocusable = cardRef.current?.querySelector<HTMLElement>(
        'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
      );
      firstFocusable?.focus();
    });
  }, [open]);

  return (
    <AnimatePresence>
      {open && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby={title ? 'admin-modal-title' : undefined}
          className="fixed inset-0 z-[100] flex items-center justify-center p-4"
          dir="rtl"
        >
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="absolute inset-0 bg-black/60 backdrop-blur-sm"
            onClick={onClose}
            aria-hidden="true"
          />

          {/* Dialog Card */}
          <motion.div
            ref={cardRef}
            initial={{ opacity: 0, scale: 0.95, y: 12 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 12 }}
            transition={{ type: 'spring', stiffness: 400, damping: 30 }}
            className={`relative z-10 w-full ${sizeMap[size]} rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-6 shadow-2xl`}
          >
            {/* Close button */}
            <button
              type="button"
              onClick={onClose}
              aria-label="إغلاق"
              className="absolute top-5 left-5 rounded-full p-2 text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
            >
              <X className="h-4 w-4" />
            </button>

            {/* Title */}
            {title && (
              <h2
                id="admin-modal-title"
                className="mb-4 text-xl font-black text-[var(--admin-text)] text-center"
              >
                {title}
              </h2>
            )}

            {children}
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}

/* ── ConfirmModal ──────────────────────────────────────────────────── */

interface ConfirmModalProps {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  description?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'danger' | 'primary' | 'ghost';
  loading?: boolean;
}

export function ConfirmModal({
  open,
  onClose,
  onConfirm,
  title,
  description,
  confirmLabel = 'تأكيد',
  cancelLabel = 'إلغاء',
  variant = 'primary',
  loading = false,
}: ConfirmModalProps) {
  return (
    <AdminModal open={open} onClose={onClose} title={title} size="sm">
      {description && (
        <p className="mb-6 text-sm leading-relaxed text-[var(--admin-muted)] text-center font-medium">
          {description}
        </p>
      )}

      <div className="flex items-center justify-center gap-3">
        <NeumorphButton intent="ghost" size="md" pill onClick={onClose}>
          {cancelLabel}
        </NeumorphButton>
        <NeumorphButton
          intent={variant === 'ghost' ? 'primary' : variant}
          size="md"
          pill
          onClick={onConfirm}
          loading={loading}
        >
          {confirmLabel}
        </NeumorphButton>
      </div>
    </AdminModal>
  );
}

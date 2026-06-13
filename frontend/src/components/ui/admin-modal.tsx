'use client';

import { useEffect, useId, useRef, type ReactNode } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X } from 'lucide-react';
import NeumorphButton from '@/components/ui/neumorph-button';

/* ── Size map ──────────────────────────────────────────────────────── */

const sizeMap = {
  sm: 'max-w-sm',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
} as const;

const focusableSelector = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[contenteditable="true"]',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

function getFocusableElements(container: HTMLElement) {
  return Array.from(container.querySelectorAll<HTMLElement>(focusableSelector)).filter(
    (element) => element.getClientRects().length > 0 && element.getAttribute('aria-hidden') !== 'true'
  );
}

function isTopmostDialog(dialog: HTMLElement) {
  const openDialogs = Array.from(
    document.querySelectorAll<HTMLElement>('[role="dialog"][aria-modal="true"]')
  ).filter((candidate) => candidate.getClientRects().length > 0);
  return openDialogs[openDialogs.length - 1] === dialog;
}

function makeOutsideContentInert(modalRoot: HTMLElement) {
  const snapshots: Array<{
    element: HTMLElement;
    inert: boolean;
    ariaHidden: string | null;
  }> = [];
  let current: HTMLElement | null = modalRoot;

  while (current && current !== document.body) {
    const parent: HTMLElement | null = current.parentElement;
    if (!parent) break;

    Array.from(parent.children).forEach((sibling) => {
      if (sibling === current || !(sibling instanceof HTMLElement)) return;
      snapshots.push({
        element: sibling,
        inert: sibling.inert,
        ariaHidden: sibling.getAttribute('aria-hidden'),
      });
      sibling.inert = true;
      sibling.setAttribute('aria-hidden', 'true');
    });

    current = parent;
  }

  return () => {
    snapshots.reverse().forEach(({ element, inert, ariaHidden }) => {
      element.inert = inert;
      if (ariaHidden === null) element.removeAttribute('aria-hidden');
      else element.setAttribute('aria-hidden', ariaHidden);
    });
  };
}

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
  const modalRootRef = useRef<HTMLDivElement>(null);
  const previouslyFocusedRef = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);
  const titleId = useId();

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useEffect(() => {
    if (!open) return;

    const card = cardRef.current;
    const modalRoot = modalRootRef.current;
    if (!card || !modalRoot) return;

    previouslyFocusedRef.current = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    const restoreOutsideContent = makeOutsideContentInert(modalRoot);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const handleKeyDown = (e: KeyboardEvent) => {
      if (!isTopmostDialog(card)) return;

      if (e.key === 'Escape') {
        e.preventDefault();
        onCloseRef.current();
        return;
      }

      if (e.key !== 'Tab') return;

      const focusableElements = getFocusableElements(card);
      if (focusableElements.length === 0) {
        e.preventDefault();
        card.focus();
        return;
      }

      const firstFocusable = focusableElements[0];
      const lastFocusable = focusableElements[focusableElements.length - 1];
      const activeElement = document.activeElement;

      if (e.shiftKey && (activeElement === firstFocusable || !card.contains(activeElement))) {
        e.preventDefault();
        lastFocusable.focus();
      } else if (!e.shiftKey && activeElement === lastFocusable) {
        e.preventDefault();
        firstFocusable.focus();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    const frameId = requestAnimationFrame(() => {
      const [firstFocusable] = getFocusableElements(card);
      (firstFocusable ?? card).focus();
    });

    return () => {
      cancelAnimationFrame(frameId);
      document.removeEventListener('keydown', handleKeyDown);
      restoreOutsideContent();
      document.body.style.overflow = previousOverflow;
      const previouslyFocused = previouslyFocusedRef.current;
      if (previouslyFocused?.isConnected) previouslyFocused.focus();
    };
  }, [open]);

  return (
    <AnimatePresence>
      {open && (
        <div
          ref={modalRootRef}
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
            role="dialog"
            aria-modal="true"
            aria-label={title ? undefined : 'نافذة حوار'}
            aria-labelledby={title ? titleId : undefined}
            tabIndex={-1}
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
                id={titleId}
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

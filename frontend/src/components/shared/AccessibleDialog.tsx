'use client';

import React, { useEffect, useId, useRef } from 'react';
import { AnimatePresence, motion, useReducedMotion } from 'framer-motion';

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

export interface AccessibleDialogProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  subtitle?: string;
  className?: string;
  children: React.ReactNode;
}

export function AccessibleDialog({
  open,
  onClose,
  title,
  subtitle,
  className = '',
  children,
}: AccessibleDialogProps) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const modalRootRef = useRef<HTMLDivElement>(null);
  const previouslyFocusedRef = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);
  const titleId = useId();
  const subtitleId = useId();
  const prefersReducedMotion = useReducedMotion();

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useEffect(() => {
    if (!open) return;

    const dialog = dialogRef.current;
    const modalRoot = modalRootRef.current;
    if (!dialog || !modalRoot) return;

    previouslyFocusedRef.current = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    const restoreOutsideContent = makeOutsideContentInert(modalRoot);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const handleKeyDown = (e: KeyboardEvent) => {
      if (!isTopmostDialog(dialog)) return;

      if (e.key === 'Escape') {
        e.preventDefault();
        onCloseRef.current();
        return;
      }

      if (e.key !== 'Tab') return;

      const focusableElements = getFocusableElements(dialog);
      if (focusableElements.length === 0) {
        e.preventDefault();
        dialog.focus();
        return;
      }

      const firstFocusable = focusableElements[0];
      const lastFocusable = focusableElements[focusableElements.length - 1];
      const activeElement = document.activeElement;

      if (e.shiftKey && (activeElement === firstFocusable || !dialog.contains(activeElement))) {
        e.preventDefault();
        lastFocusable.focus();
      } else if (!e.shiftKey && activeElement === lastFocusable) {
        e.preventDefault();
        firstFocusable.focus();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    const frameId = requestAnimationFrame(() => {
      const [firstFocusable] = getFocusableElements(dialog);
      (firstFocusable ?? dialog).focus();
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

  // Framer motion variants respecting reduced motion settings
  const backdropVariants = {
    hidden: { opacity: 0 },
    visible: { opacity: 1 },
  };

  const dialogVariants = {
    hidden: prefersReducedMotion ? { opacity: 0 } : { opacity: 0, scale: 0.95, y: 16 },
    visible: prefersReducedMotion ? { opacity: 1 } : { opacity: 1, scale: 1, y: 0 },
  };

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          ref={modalRootRef}
          initial="hidden"
          animate="visible"
          exit="hidden"
          variants={backdropVariants}
          transition={{ duration: prefersReducedMotion ? 0.05 : 0.2 }}
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[color:rgba(28,28,22,0.5)] backdrop-blur-sm"
          onClick={onClose}
        >
          <motion.div
            ref={dialogRef}
            initial="hidden"
            animate="visible"
            exit="hidden"
            variants={dialogVariants}
            transition={{
              duration: prefersReducedMotion ? 0.05 : 0.3,
              ease: [0.16, 1, 0.3, 1]
            }}
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-label={title ? undefined : 'نافذة حوار'}
            aria-labelledby={title ? titleId : undefined}
            aria-describedby={subtitle ? subtitleId : undefined}
            tabIndex={-1}
            className={`relative flex max-h-[90vh] w-full flex-col outline-none ${className}`}
          >
            {title && (
              <div className="mb-4">
                <h2 id={titleId} className="text-xl font-black text-[var(--admin-text)]">{title}</h2>
                {subtitle && <p id={subtitleId} className="text-sm font-medium text-[var(--admin-muted)] mt-1">{subtitle}</p>}
              </div>
            )}
            {children}
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

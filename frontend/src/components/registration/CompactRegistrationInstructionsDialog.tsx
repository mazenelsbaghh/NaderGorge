'use client';

import { useEffect, useRef } from 'react';

import {
  LOGIN_INSTRUCTION_COPY,
  LOGIN_INSTRUCTION_NOTE,
  REGISTRATION_INSTRUCTION_COPY,
  REGISTRATION_INSTRUCTION_NOTE,
} from './registration-instruction-copy';

type InstructionMode = 'register' | 'login';

type CompactRegistrationInstructionsDialogProps = {
  open: boolean;
  onClose: () => void;
  confirmLabel?: string;
  title?: string;
  subtitle?: string;
  mode?: InstructionMode;
};

export function CompactRegistrationInstructionsDialog({
  open,
  onClose,
  confirmLabel = 'فهمت وموافق على الشروط',
  title = 'تعليمات وشروط هامة قبل التسجيل',
  subtitle = 'يرجى قراءتها بدقة قبل إنشاء الحساب أو تسجيل الدخول لأول مرة.',
  mode = 'register',
}: CompactRegistrationInstructionsDialogProps) {
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const previousFocus = document.activeElement as HTMLElement | null;
    dialogRef.current?.focus();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      previousFocus?.focus();
    };
  }, [onClose, open]);

  if (!open) return null;

  const instructions =
    mode === 'login' ? LOGIN_INSTRUCTION_COPY : REGISTRATION_INSTRUCTION_COPY;
  const instructionNote =
    mode === 'login' ? LOGIN_INSTRUCTION_NOTE : REGISTRATION_INSTRUCTION_NOTE;

  return (
    <div
      className="fixed inset-0 z-[var(--z-modal)] flex items-center justify-center bg-black/75 p-4 backdrop-blur-[4px]"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="compact-instructions-title"
        aria-describedby="compact-instructions-subtitle"
        tabIndex={-1}
        dir="rtl"
        className="flex max-h-[85vh] w-full max-w-2xl flex-col overflow-hidden rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)] shadow-2xl outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
      >
        <header className="flex items-start justify-between gap-4 border-b border-[var(--admin-border)] p-5 sm:p-6">
          <div>
            <p className="mb-1 text-xs font-black text-[var(--admin-primary)]">
              قبل المتابعة
            </p>
            <h2 id="compact-instructions-title" className="text-xl font-black">
              {title}
            </h2>
            <p
              id="compact-instructions-subtitle"
              className="mt-1 text-xs font-bold leading-5 text-[var(--admin-muted)]"
            >
              {subtitle}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="إغلاق"
            className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-xl text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
          >
            ×
          </button>
        </header>

        <div className="custom-scrollbar flex-1 overflow-y-auto p-5 sm:p-6">
          <ol className="grid gap-3 sm:grid-cols-2">
            {instructions.map((instruction, index) => (
              <li
                key={instruction.key}
                className="flex gap-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4"
              >
                <span className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-[var(--admin-primary-15)] text-sm font-black text-[var(--admin-primary)]">
                  {index + 1}
                </span>
                <span>
                  <strong className="block text-sm font-black">
                    {instruction.title}
                  </strong>
                  <span className="mt-1 block text-xs font-semibold leading-relaxed text-[var(--admin-muted)]">
                    {instruction.description}
                  </span>
                </span>
              </li>
            ))}
          </ol>

          <p className="mt-4 rounded-2xl border border-amber-500/20 bg-amber-500/5 p-4 text-xs font-semibold leading-relaxed text-amber-600 dark:text-amber-300">
            {instructionNote}
          </p>
        </div>

        <footer className="border-t border-[var(--admin-border)] bg-[var(--admin-card-soft)]/50 p-5 sm:p-6">
          <button
            type="button"
            onClick={onClose}
            className="min-h-12 w-full rounded-2xl bg-[var(--admin-primary)] px-8 text-sm font-black text-[var(--admin-primary-contrast)] transition-[filter,transform] hover:brightness-110 active:scale-[0.98] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] sm:w-auto"
          >
            {confirmLabel}
          </button>
        </footer>
      </div>
    </div>
  );
}

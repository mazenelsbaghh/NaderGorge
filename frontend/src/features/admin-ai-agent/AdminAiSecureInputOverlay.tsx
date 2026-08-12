'use client';
import { useEffect, useRef, useState } from 'react';
import type { AdminAiSecureInputKind } from '@/services/admin-ai-agent-contract';

const focusableSelector =
  'button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])';

function containTabFocus(event: KeyboardEvent, container: HTMLElement | null) {
  if (event.key !== 'Tab' || !container) return;
  const focusableElements = Array.from(
    container.querySelectorAll<HTMLElement>(focusableSelector)
  );
  if (focusableElements.length === 0) {
    event.preventDefault();
    container.focus();
    return;
  }
  const first = focusableElements[0];
  const last = focusableElements[focusableElements.length - 1];
  const activeElement = document.activeElement;
  const leavingStart =
    event.shiftKey &&
    (activeElement === first || !container.contains(activeElement));
  const leavingEnd = !event.shiftKey && activeElement === last;
  if (!leavingStart && !leavingEnd) return;
  event.preventDefault();
  (leavingStart ? last : first).focus();
}

function restoreFocus(element: HTMLElement | null) {
  if (!element?.isConnected) return;
  requestAnimationFrame(() =>
    requestAnimationFrame(() => {
      if (element.isConnected) element.focus({ preventScroll: true });
    })
  );
}

export function AdminAiSecureInputOverlay({
  kind,
  open,
  busy,
  onClose,
  onSubmit,
}: {
  kind: AdminAiSecureInputKind;
  open: boolean;
  busy: boolean;
  onClose: () => void;
  onSubmit: (secureValue: string) => Promise<void>;
}) {
  const [secureValue, setSecureValue] = useState('');
  const secureInputRef = useRef<HTMLInputElement>(null);
  const dialogRef = useRef<HTMLDivElement>(null);
  const restoreFocusTo = useRef<HTMLElement | null>(null);
  const busyRef = useRef(busy);
  const onCloseRef = useRef(onClose);

  useEffect(() => {
    busyRef.current = busy;
    onCloseRef.current = onClose;
  }, [busy, onClose]);

  useEffect(() => {
    if (!open) return;

    restoreFocusTo.current =
      document.activeElement instanceof HTMLElement &&
      document.activeElement !== document.body
        ? document.activeElement
        : document.querySelector<HTMLElement>('[data-admin-ai-secure-trigger]');
    setSecureValue('');
    queueMicrotask(() => secureInputRef.current?.focus());

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !busyRef.current) {
        event.preventDefault();
        setSecureValue('');
        onCloseRef.current();
        return;
      }
      containTabFocus(event, dialogRef.current);
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      const trigger = restoreFocusTo.current;
      restoreFocusTo.current = null;
      restoreFocus(trigger);
    };
  }, [open]);
  if (!open) return null;
  const labels: Record<AdminAiSecureInputKind, string> = {
    Password: 'كلمة المرور',
    PrivateFile: 'رمز الملف الخاص',
    ProtectedToken: 'التوكن المحمي',
    VerificationAnswer: 'إجابة التحقق',
  };
  return (
    <div
      ref={dialogRef}
      role="dialog"
      aria-modal="true"
      aria-labelledby="secure-title"
      aria-describedby="secure-description"
      tabIndex={-1}
      className="fixed inset-0 z-50 grid place-items-center bg-[color-mix(in_srgb,var(--admin-text)_60%,transparent)] p-4"
    >
      <form
        className="w-full max-w-md rounded-2xl bg-[var(--admin-card)] p-5 shadow-2xl"
        onSubmit={async (e) => {
          e.preventDefault();
          await onSubmit(secureValue);
          setSecureValue('');
        }}
      >
        <h2 id="secure-title" className="text-lg font-black">
          إدخال آمن: {labels[kind]}
        </h2>
        <p
          id="secure-description"
          className="mt-2 text-sm text-[var(--admin-muted)]"
        >
          هذه القيمة تُرسل مباشرة لمسار الحماية ولا تدخل المحادثة أو ذاكرة
          الواجهة.
        </p>
        <input
          ref={secureInputRef}
          type={kind === 'Password' ? 'password' : 'text'}
          value={secureValue}
          onChange={(e) => setSecureValue(e.target.value)}
          autoComplete="off"
          aria-label={labels[kind]}
          className="mt-4 min-h-12 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4"
        />
        <div className="mt-4 flex gap-2">
          <button
            type="submit"
            disabled={!secureValue || busy}
            className="min-h-11 flex-1 rounded-xl bg-[var(--admin-primary)] font-black text-[var(--admin-primary-contrast)] disabled:opacity-50"
          >
            إرسال بأمان
          </button>
          <button
            type="button"
            onClick={() => {
              setSecureValue('');
              onCloseRef.current();
            }}
            className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 font-bold"
          >
            إلغاء
          </button>
        </div>
      </form>
    </div>
  );
}

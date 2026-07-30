'use client';

import {
  type CSSProperties,
  type ReactNode,
  type RefObject,
  useEffect,
  useId,
  useRef,
  useState,
} from 'react';
import { createPortal } from 'react-dom';

const FOCUSABLE_SELECTOR = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

type AccessibleOverlayProps = {
  open: boolean;
  onClose: () => void;
  children: ReactNode;
  label?: string;
  labelledBy?: string;
  className?: string;
  backdropClassName?: string;
  layerClassName?: string;
  initialFocusRef?: RefObject<HTMLElement | null>;
  triggerRef?: RefObject<HTMLElement | null>;
  style?: CSSProperties;
  testId?: string;
};

export function AccessibleOverlay({
  open,
  onClose,
  children,
  label,
  labelledBy,
  className = '',
  backdropClassName = '',
  layerClassName = '',
  initialFocusRef,
  triggerRef,
  style,
  testId,
}: AccessibleOverlayProps) {
  const generatedLabelId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const restoreFocusRef = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);
  const [host, setHost] = useState<HTMLDivElement | null>(null);

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useEffect(() => {
    const portalHost = document.createElement('div');
    portalHost.dataset.accessibleOverlayRoot = 'true';
    document.body.appendChild(portalHost);
    setHost(portalHost);
    return () => {
      portalHost.remove();
    };
  }, []);

  useEffect(() => {
    if (!open || !host) return;

    restoreFocusRef.current =
      triggerRef?.current ??
      (document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const siblings = Array.from(document.body.children).filter(
      (element) => element !== host,
    );
    const siblingState = siblings.map((element) => ({
      element,
      inert: (element as HTMLElement).inert,
      ariaHidden: element.getAttribute('aria-hidden'),
    }));
    for (const sibling of siblings) {
      (sibling as HTMLElement).inert = true;
      sibling.setAttribute('aria-hidden', 'true');
    }

    const focusDialog = () => {
      const target =
        initialFocusRef?.current ??
        dialogRef.current?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR) ??
        dialogRef.current;
      target?.focus({ preventScroll: true });
    };
    const frame = window.requestAnimationFrame(focusDialog);

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        onCloseRef.current();
        return;
      }
      if (event.key !== 'Tab' || !dialogRef.current) return;

      const focusable = Array.from(
        dialogRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
      ).filter(
        (element) =>
          !element.hidden &&
          element.getAttribute('aria-hidden') !== 'true' &&
          element.getClientRects().length > 0,
      );
      if (focusable.length === 0) {
        event.preventDefault();
        dialogRef.current.focus();
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', onKeyDown, true);

    return () => {
      window.cancelAnimationFrame(frame);
      document.removeEventListener('keydown', onKeyDown, true);
      document.body.style.overflow = previousOverflow;
      for (const state of siblingState) {
        (state.element as HTMLElement).inert = state.inert;
        if (state.ariaHidden === null) {
          state.element.removeAttribute('aria-hidden');
        } else {
          state.element.setAttribute('aria-hidden', state.ariaHidden);
        }
      }
      window.requestAnimationFrame(() => {
        if (restoreFocusRef.current?.isConnected) {
          restoreFocusRef.current.focus({ preventScroll: true });
        }
      });
    };
  }, [host, initialFocusRef, open, triggerRef]);

  if (!open || !host) return null;

  const content = (
    <div
      className={`fixed inset-0 z-[100] ${layerClassName}`.trim()}
      data-testid={testId}
    >
      <button
        type="button"
        tabIndex={-1}
        className={`absolute inset-0 cursor-default bg-black/40 ${backdropClassName}`.trim()}
        aria-label="إغلاق النافذة"
        onClick={onClose}
      />
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-label={label}
        aria-labelledby={labelledBy ?? (label ? undefined : generatedLabelId)}
        tabIndex={-1}
        className={`absolute outline-none ${className}`.trim()}
        style={style}
      >
        {!label && !labelledBy ? (
          <span id={generatedLabelId} className="sr-only">
            نافذة
          </span>
        ) : null}
        {children}
      </div>
    </div>
  );

  return createPortal(content, host);
}

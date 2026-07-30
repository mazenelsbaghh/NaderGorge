'use client';

import type { ReactNode } from 'react';
import Link from 'next/link';
import {
  AlertTriangle,
  Inbox,
  LoaderCircle,
  RefreshCw,
} from 'lucide-react';

type AsyncRegionStateProps = {
  status: 'loading' | 'empty' | 'error';
  title?: string;
  message?: string;
  children?: ReactNode;
  onRetry?: () => void;
  homeHref?: string;
  homeLabel?: string;
  className?: string;
};

export function AsyncRegionState({
  status,
  title,
  message,
  children,
  onRetry,
  homeHref,
  homeLabel = 'العودة للرئيسية',
  className = '',
}: AsyncRegionStateProps) {
  if (status === 'loading') {
    return (
      <section
        role="status"
        aria-live="polite"
        aria-busy="true"
        className={className}
      >
        <span className="sr-only">{message ?? 'جاري تحميل المحتوى'}</span>
        {children ?? (
          <div className="flex min-h-40 items-center justify-center">
            <LoaderCircle
              className="h-7 w-7 animate-spin text-[var(--admin-primary)]"
              aria-hidden="true"
            />
          </div>
        )}
      </section>
    );
  }

  const isError = status === 'error';
  const Icon = isError ? AlertTriangle : Inbox;
  return (
    <section
      role={isError ? 'alert' : 'status'}
      aria-live={isError ? 'assertive' : 'polite'}
      className={`flex min-h-[40vh] items-center justify-center px-4 py-8 ${className}`.trim()}
    >
      <div className="w-full max-w-md rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 text-center sm:p-8">
        <div
          className={`mx-auto mb-5 flex h-14 w-14 items-center justify-center rounded-full ${
            isError
              ? 'bg-[var(--admin-danger)]/10 text-[var(--admin-danger)]'
              : 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]'
          }`}
        >
          <Icon className="h-7 w-7" aria-hidden="true" />
        </div>
        <h2 className="text-balance text-xl font-black text-[var(--admin-text)]">
          {title ?? (isError ? 'تعذّر تحميل المحتوى' : 'لا توجد بيانات بعد')}
        </h2>
        <p className="mx-auto mt-2 max-w-sm [overflow-wrap:anywhere] text-sm font-medium leading-7 text-[var(--admin-muted)]">
          {message ??
            (isError
              ? 'حدثت مشكلة مؤقتة. حاول مرة أخرى، وإذا استمرت تواصل مع الدعم.'
              : 'سيظهر المحتوى هنا عند توفره.')}
        </p>
        {isError && (onRetry || homeHref) ? (
          <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
            {onRetry ? (
              <button
                type="button"
                onClick={onRetry}
                className="inline-flex min-h-11 items-center gap-2 rounded-full bg-[var(--admin-primary)] px-6 text-sm font-bold text-[var(--admin-primary-contrast)]"
              >
                <RefreshCw className="h-4 w-4" aria-hidden="true" />
                حاول مرة أخرى
              </button>
            ) : null}
            {homeHref ? (
              <Link
                href={homeHref}
                className="inline-flex min-h-11 items-center rounded-full border border-[var(--admin-border)] px-6 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]"
              >
                {homeLabel}
              </Link>
            ) : null}
          </div>
        ) : null}
      </div>
    </section>
  );
}

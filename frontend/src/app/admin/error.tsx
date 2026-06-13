'use client';

import Link from 'next/link';
import { AlertTriangle, RefreshCw, Home } from 'lucide-react';
import { useAdminTheme } from '@/components/admin/useAdminTheme';

export default function AdminError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const { themeVars } = useAdminTheme();

  return (
    <div
      dir="rtl"
      className="flex min-h-dvh items-center justify-center bg-[var(--admin-bg)] p-6"
      style={themeVars}
    >
      <div className="w-full max-w-md rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 text-center shadow-2xl">
        {/* Icon */}
        <div className="mx-auto mb-5 flex h-14 w-14 items-center justify-center rounded-full bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-400">
          <AlertTriangle className="h-7 w-7" />
        </div>

        {/* Title */}
        <h2 className="mb-2 text-xl font-black text-[var(--admin-text)]">
          حدث خطأ غير متوقع
        </h2>

        {/* Error message */}
        <p className="mb-6 text-sm leading-relaxed text-[var(--admin-muted)] font-medium">
          {error.message || 'حدث خطأ أثناء تحميل هذه الصفحة.'}
        </p>

        {/* Actions */}
        <div className="flex items-center justify-center gap-3">
          <button
            onClick={reset}
            className="flex h-11 items-center gap-2 rounded-full bg-[var(--admin-primary)] px-6 text-sm font-bold text-[var(--admin-primary-contrast)] shadow-md transition-all hover:brightness-110 active:scale-95"
          >
            <RefreshCw className="h-4 w-4" />
            حاول مرة أخرى
          </button>
          <Link
            href="/admin"
            prefetch={false}
            className="flex h-11 items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-6 text-sm font-bold text-[var(--admin-text)] shadow-sm transition-all hover:bg-[var(--admin-hover)]"
          >
            <Home className="h-4 w-4" />
            العودة للرئيسية
          </Link>
        </div>
      </div>
    </div>
  );
}

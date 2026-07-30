'use client';

import { Copy } from 'lucide-react';
import toast from 'react-hot-toast';

interface ContentInternalCodeProps {
  code: string;
  label?: string;
  compact?: boolean;
}

export function ContentInternalCode({ code, label = 'الكود الداخلي', compact = false }: ContentInternalCodeProps) {
  const copyCode = async () => {
    try {
      await navigator.clipboard.writeText(code);
      toast.success('تم نسخ الكود الداخلي');
    } catch {
      toast.error('تعذر نسخ الكود الداخلي');
    }
  };

  return (
    <div className={`inline-flex min-w-0 items-center gap-2 rounded-lg bg-[var(--admin-card-strong)] ${compact ? 'px-2 py-1' : 'px-3 py-2'}`}>
      <div className="min-w-0">
        {!compact && <div className="text-xs font-medium text-[var(--admin-muted)]">{label}</div>}
        <code className="block max-w-full truncate text-xs font-semibold text-[var(--admin-text)]" title={code}>
          {code}
        </code>
      </div>
      <button
        type="button"
        onClick={copyCode}
        className="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-md text-[var(--admin-primary)] transition-colors duration-200 hover:bg-[var(--admin-primary-15)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
        aria-label={`نسخ ${label}: ${code}`}
        title={`نسخ ${label}`}
      >
        <Copy className="h-4 w-4" aria-hidden="true" />
      </button>
    </div>
  );
}

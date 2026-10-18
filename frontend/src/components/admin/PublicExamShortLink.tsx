'use client';

import { useState } from 'react';
import { Copy } from 'lucide-react';
import { getSurfaceOrigins } from '@/packages/surface-runtime/config';

export function PublicExamShortLink({ slug }: { slug: string }) {
  const [message, setMessage] = useState('');
  const [copying, setCopying] = useState(false);
  const path = `/public-exams/${encodeURIComponent(slug)}`;

  async function copyLink() {
    setCopying(true);
    try {
      await navigator.clipboard.writeText(`${getSurfaceOrigins().student}${path}`);
      setMessage('تم نسخ الرابط. دخول الامتحان يتطلب حساب طالب.');
    } catch {
      setMessage('تعذر النسخ. أضف المسار الظاهر إلى عنوان موقع الطلاب لمشاركته.');
    } finally {
      setCopying(false);
    }
  }

  return (
    <div className="mt-2 space-y-1 text-sm text-[var(--admin-text)]">
      <div className="flex flex-wrap items-center gap-x-3">
        <span dir="ltr" className="break-all select-all">/public-exams/{slug}</span>
        <button type="button" disabled={copying} onClick={() => void copyLink()} className="inline-flex min-h-11 items-center gap-1.5 rounded-md px-2 font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)] disabled:opacity-50">
          <Copy className="h-4 w-4" aria-hidden="true" />نسخ رابط الامتحان
        </button>
      </div>
      {message && <p role="status" className="text-xs leading-6 text-[var(--admin-muted)]">{message}</p>}
    </div>
  );
}

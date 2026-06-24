'use client';

import { useState, useEffect } from 'react';
import { Copy, Check, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { useStudentShellStore } from '@/stores/student-shell-store';

export function HeaderParentBadge() {
  const parentTrackingCode = useStudentShellStore((state) => state.parentTrackingCode);
  const [copied, setCopied] = useState(false);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) return null;
  if (!parentTrackingCode) return null;

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(parentTrackingCode);
      setCopied(true);
      toast.success('تم نسخ رمز المتابعة بنجاح!');
      setTimeout(() => setCopied(false), 2000);
    } catch {
      toast.error('فشل نسخ الرمز. يرجى نسخه يدوياً.');
    }
  };

  return (
    <button
      type="button"
      onClick={handleCopy}
      data-testid="header-parent-badge"
      className="group relative flex h-10 items-center gap-1.5 sm:gap-2 rounded-full border border-[var(--admin-primary-15)] bg-[var(--admin-card-soft)] px-3 sm:px-4 text-xs font-bold text-[var(--admin-primary)] transition-all hover:bg-[var(--admin-primary-15)] hover:-translate-y-0.5 active:scale-95 shadow-sm"
      title="انقر لنسخ رمز متابعة ولي الأمر"
    >
      <Users className="h-3.5 w-3.5 text-[var(--admin-primary)] shrink-0" />
      <span className="hidden sm:inline">رمز المتابعة:</span>
      <span className="sm:hidden">متابعة:</span>
      <span className="font-mono font-black tracking-wider bg-[var(--admin-card-strong)] px-1.5 py-0.5 rounded text-sm select-all">
        {parentTrackingCode}
      </span>
      {copied ? (
        <Check className="h-3.5 w-3.5 text-emerald-500 shrink-0 transition duration-300" />
      ) : (
        <Copy className="h-3.5 w-3.5 opacity-60 group-hover:opacity-100 shrink-0 transition-opacity" />
      )}
    </button>
  );
}

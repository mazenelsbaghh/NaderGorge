'use client';

import { useEffect, useMemo } from 'react';
import { RefreshCw } from 'lucide-react';
import { Dropdown } from '@/components/ui/dropdown';
import { useVideoTypes } from '@/hooks/useVideoTypes';

interface VideoTypeSelectProps {
  value: string;
  onChange: (value: string) => void;
  onAvailabilityChange?: (available: boolean) => void;
  label?: string;
  currentTypeId?: string;
}

export function VideoTypeSelect({
  value,
  onChange,
  onAvailabilityChange,
  label = 'نوع الفيديو',
  currentTypeId,
}: VideoTypeSelectProps) {
  const { types, loading, error, retry } = useVideoTypes(true);
  const options = useMemo(
    () => types
      .filter((type) => type.isActive || type.id === currentTypeId)
      .map((type) => ({
        value: type.id,
        label: type.isActive ? type.name : `${type.name} (معطل، مستخدم حالياً)`,
      })),
    [currentTypeId, types],
  );
  const available = !loading && !error && options.length > 0;

  useEffect(() => {
    onAvailabilityChange?.(available);
  }, [available, onAvailabilityChange]);

  if (loading) {
    return (
      <div className="space-y-2" aria-label="جاري تحميل أنواع الفيديو">
        <div className="h-4 w-20 animate-pulse rounded bg-[var(--admin-card-soft)]" />
        <div className="h-11 w-full animate-pulse rounded-lg bg-[var(--admin-card-soft)]" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex min-h-11 items-center justify-between gap-3 rounded-lg bg-red-500/10 px-3 py-2 text-xs font-bold text-red-700 dark:text-red-300">
        <span>{error}</span>
        <button type="button" onClick={() => void retry()} className="flex h-8 cursor-pointer items-center gap-1 rounded-md px-2 hover:bg-red-500/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-500">
          <RefreshCw className="h-3.5 w-3.5" /> إعادة المحاولة
        </button>
      </div>
    );
  }

  if (options.length === 0) {
    return (
      <p className="rounded-lg bg-amber-500/10 px-3 py-2 text-xs font-bold text-amber-800 dark:text-amber-300">
        لا يوجد نوع فيديو نشط. فعّل نوعاً من صفحة إدارة الأنواع أولاً.
      </p>
    );
  }

  return (
    <Dropdown
      label={label}
      value={value}
      onChange={(next) => onChange(String(next))}
      size="sm"
      options={[{ value: '', label: 'اختر نوع الفيديو' }, ...options]}
      error={!value ? 'اختيار نوع الفيديو مطلوب.' : undefined}
    />
  );
}

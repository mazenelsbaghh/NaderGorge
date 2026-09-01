'use client';

import { useEffect, useMemo, useState } from 'react';
import { Library, RefreshCw } from 'lucide-react';

import { Dropdown } from '@/components/ui/dropdown';
import { getApiErrorSummary } from '@/lib/api-errors';
import {
  adminService,
  type BunnyLibraryReferenceDto,
} from '@/services/admin-service';

interface BunnyLibrarySelectProps {
  value: string;
  onChange: (libraryId: string) => void;
  detectedLibraryId?: string | null;
  currentLibrary?: BunnyLibraryReferenceDto | null;
  onAvailabilityChange?: (available: boolean) => void;
  label?: string;
  disabled?: boolean;
}

function libraryOptionLabel(library: BunnyLibraryReferenceDto, isCurrentOnly: boolean) {
  const currentStatus = !library.isActive
    ? 'معطلة، مستخدمة حاليًا'
    : !library.apiKeyConfigured
      ? 'مفتاح مطلوب، مستخدمة حاليًا'
      : 'مستخدمة حاليًا';
  const status = isCurrentOnly ? ` · ${currentStatus}` : '';
  return `${library.name} · ${library.libraryId}${status}`;
}

export function BunnyLibrarySelect({
  value,
  onChange,
  detectedLibraryId = null,
  currentLibrary,
  onAvailabilityChange,
  label = 'مكتبة Bunny (مطلوب)',
  disabled = false,
}: BunnyLibrarySelectProps) {
  const [libraries, setLibraries] = useState<BunnyLibraryReferenceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadVersion, setReloadVersion] = useState(0);

  useEffect(() => {
    let isCurrentRequest = true;
    setLoading(true);
    setLoadError(null);

    adminService.listAvailableBunnyLibraries()
      .then((availableLibraries) => {
        if (isCurrentRequest) setLibraries(availableLibraries);
      })
      .catch((requestError: unknown) => {
        if (isCurrentRequest) {
          setLibraries([]);
          setLoadError(getApiErrorSummary(requestError, 'تعذر تحميل مكتبات Bunny المتاحة.'));
        }
      })
      .finally(() => {
        if (isCurrentRequest) setLoading(false);
      });

    return () => { isCurrentRequest = false; };
  }, [reloadVersion]);

  const selectableLibraries = useMemo(() => {
    if (!currentLibrary || libraries.some((library) => library.id === currentLibrary.id)) {
      return libraries;
    }
    return [...libraries, currentLibrary];
  }, [currentLibrary, libraries]);

  const detectedLibrary = detectedLibraryId
    ? selectableLibraries.find((library) => library.libraryId === detectedLibraryId)
    : undefined;
  const selectedLibrary = selectableLibraries.find((library) => library.id === value);
  const selectedCurrentLibrary = Boolean(currentLibrary && selectedLibrary?.id === currentLibrary.id);
  const selectionAvailable = Boolean(
    selectedLibrary &&
    (selectedCurrentLibrary || (selectedLibrary.isActive && selectedLibrary.apiKeyConfigured))
  );

  useEffect(() => {
    if (!detectedLibraryId || loading || loadError) return;
    const detectedSelection = detectedLibrary?.id ?? '';
    if (value !== detectedSelection) onChange(detectedSelection);
  }, [detectedLibrary, detectedLibraryId, loadError, loading, onChange, value]);

  useEffect(() => {
    onAvailabilityChange?.(selectionAvailable);
  }, [onAvailabilityChange, selectionAvailable]);

  if (loading) {
    return (
      <div className="space-y-2" aria-label="جاري تحميل مكتبات Bunny">
        <div className="h-4 w-24 animate-pulse rounded bg-[var(--admin-card-soft)]" />
        <div className="h-11 w-full animate-pulse rounded-xl bg-[var(--admin-card-soft)]" />
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="flex min-h-11 items-center justify-between gap-3 rounded-xl border border-red-500/20 bg-red-500/10 px-3 py-2 text-xs font-bold text-red-700 dark:text-red-300" role="alert">
        <span>{loadError}</span>
        <button type="button" onClick={() => setReloadVersion((version) => version + 1)} className="inline-flex min-h-9 items-center gap-1 rounded-lg px-2 hover:bg-red-500/10">
          <RefreshCw className="h-3.5 w-3.5" />
          إعادة المحاولة
        </button>
      </div>
    );
  }

  if (selectableLibraries.length === 0) {
    return (
      <div className="flex items-start gap-2 rounded-xl border border-amber-500/20 bg-amber-500/10 px-3 py-3 text-xs font-bold leading-5 text-amber-800 dark:text-amber-300" role="status">
        <Library className="mt-0.5 h-4 w-4 shrink-0" />
        لا توجد مكتبة Bunny نشطة بمفتاح صالح. اطلب من مدير الإعدادات تجهيز مكتبة أولًا.
      </div>
    );
  }

  const detectedLibraryUnavailable = Boolean(detectedLibraryId && !detectedLibrary);
  const options = selectableLibraries.map((library) => {
    const isCurrentOnly = library.id === currentLibrary?.id && !libraries.some((candidate) => candidate.id === library.id);
    return {
      value: library.id,
      label: libraryOptionLabel(library, isCurrentOnly),
    };
  });

  return (
    <div className="space-y-2">
      <Dropdown
        label={label}
        value={value}
        onChange={(next) => onChange(String(next))}
        options={options}
        placeholder="اختر المكتبة لهذا الفيديو"
        searchable={options.length > 6}
        disabled={disabled || Boolean(detectedLibraryId && detectedLibrary)}
        size="sm"
        error={detectedLibraryUnavailable
          ? `المكتبة رقم ${detectedLibraryId} غير مسجلة أو غير متاحة للرفع.`
          : !selectionAvailable ? 'اختيار مكتبة Bunny متاحة مطلوب.' : undefined}
      />
      {detectedLibrary && (
        <p className="text-xs font-bold text-emerald-700 dark:text-emerald-400" role="status">
          تم تحديد مكتبة «{detectedLibrary.name}» تلقائيًا من الرابط.
        </p>
      )}
    </div>
  );
}

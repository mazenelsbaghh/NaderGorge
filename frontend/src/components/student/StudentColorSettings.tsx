'use client';

import { useState } from 'react';
import { Check, Loader2, Moon, Sun } from 'lucide-react';
import { getAvailableStudentThemePalettes, useStudentTheme } from '@/hooks/useStudentTheme';
import { type StudentThemePalette } from '@/lib/student-theme-palettes';
import { cn } from '@/lib/utils';

export function StudentColorSettings() {
  const {
    mode, updateMode, selectedLightPaletteId, selectedDarkPaletteId, updatePalette,
    isReady, isLoadingPreferences, isSavingPreferences, preferencesError, retryPreferences,
  } = useStudentTheme();
  const [saveMessage, setSaveMessage] = useState('');
  const disabled = !isReady || isSavingPreferences;

  async function saveAppearance(update: () => Promise<void>) {
    setSaveMessage('');
    try {
      await update();
      setSaveMessage('تم حفظ مظهر المنصة لحسابك.');
    } catch {
      setSaveMessage('تعذر حفظ المظهر. رجعنا لاختيارك السابق، حاول مرة أخرى.');
    }
  }

  return (
    <section aria-label="ألوان المنصة" className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h4 className="text-lg font-black text-[var(--admin-text)]">ألوان المنصة</h4>
          <p className="mt-1 text-sm text-[var(--admin-muted)]">
            اختر ألوانك لكل وضع. الاختيار يتطبق فورًا ويتحفظ تلقائيًا على حسابك.
          </p>
        </div>
        <div className="flex gap-1 rounded-xl border border-[var(--admin-border)] p-1" aria-label="وضع العرض">
          {(['light', 'dark'] as const).map((option) => {
            const Icon = option === 'light' ? Sun : Moon;
            return (
              <button
                key={option}
                type="button"
                aria-pressed={mode === option}
                disabled={disabled}
                onClick={() => void saveAppearance(() => updateMode(option))}
                className={cn(
                  'flex min-h-11 items-center gap-2 rounded-lg px-4 text-sm font-bold disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--admin-primary)]',
                  mode === option ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'text-[var(--admin-text)] hover:bg-[var(--admin-hover)]',
                )}
              >
                <Icon className="h-4 w-4" aria-hidden="true" />
                {option === 'light' ? 'الوضع الفاتح' : 'الوضع الداكن'}
              </button>
            );
          })}
        </div>
      </div>

      <div role="status" aria-live="polite" className="text-sm text-[var(--admin-muted)]">
        {isLoadingPreferences ? 'جاري تحميل ألوان حسابك...' : isSavingPreferences ? (
          <span className="flex items-center gap-2"><Loader2 className="h-4 w-4 animate-spin motion-reduce:animate-none" aria-hidden="true" />جاري حفظ المظهر...</span>
        ) : preferencesError ? (
          <span>{preferencesError} <button type="button" onClick={retryPreferences} className="min-h-11 px-2 font-bold underline">إعادة المحاولة</button></span>
        ) : saveMessage || 'تقدر تختار ألوان مختلفة للوضع الفاتح والداكن.'}
      </div>

      <div className="grid gap-8 md:grid-cols-2">
        {(['light', 'dark'] as const).map((paletteMode) => (
          <fieldset key={paletteMode} disabled={disabled} className="min-w-0 space-y-3">
            <legend className="mb-3 text-sm font-bold text-[var(--admin-text)]">
              {paletteMode === 'light' ? 'ألوان الوضع الفاتح' : 'ألوان الوضع الداكن'}
            </legend>
            {getAvailableStudentThemePalettes(paletteMode).map((palette) => (
              <PaletteChoice
                key={palette.id}
                palette={palette}
                selected={palette.id === (paletteMode === 'light' ? selectedLightPaletteId : selectedDarkPaletteId)}
                onSelect={() => void saveAppearance(() => updatePalette(paletteMode, palette.id))}
              />
            ))}
          </fieldset>
        ))}
      </div>
    </section>
  );
}

function PaletteChoice({ palette, selected, onSelect }: {
  palette: StudentThemePalette;
  selected: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      aria-label={palette.name}
      onClick={onSelect}
      className={cn(
        'flex min-h-16 w-full items-center gap-3 rounded-xl border p-3 text-right transition-colors motion-reduce:transition-none disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--admin-primary)]',
        selected ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)]' : 'border-[var(--admin-border)] hover:bg-[var(--admin-hover)]',
      )}
    >
      <span aria-hidden="true" className="flex h-11 w-20 shrink-0 items-center gap-1.5 rounded-lg border p-2" style={{ background: palette.tokens['--admin-bg'], borderColor: palette.tokens['--admin-muted'] }}>
        <span className="h-full w-3 rounded-sm" style={{ background: palette.tokens['--admin-primary'] }} />
        <span className="flex flex-1 flex-col gap-1">
          <span className="h-1.5 w-full rounded-sm" style={{ background: palette.tokens['--admin-text'] }} />
          <span className="h-1.5 w-2/3 rounded-sm" style={{ background: palette.tokens['--admin-muted'] }} />
        </span>
      </span>
      <span className="min-w-0 flex-1 font-bold text-[var(--admin-text)]">{palette.name}</span>
      {selected && <span className="flex shrink-0 items-center gap-1 text-xs font-bold text-[var(--admin-primary)]"><Check className="h-4 w-4" aria-hidden="true" />مختار</span>}
    </button>
  );
}

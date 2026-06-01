import { Check, Loader2, Palette } from 'lucide-react';
import Image from 'next/image';
import { AVATAR_LIST } from '@/data/avatars';

import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { type StudentThemeMode } from '@/lib/student-theme-palettes';
import { getAvailableStudentThemePalettes } from '@/hooks/useStudentTheme';
import { cn } from '@/lib/utils';

type StudentThemeSettingsPanelProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  selectedLightPaletteId: string;
  selectedDarkPaletteId: string;
  currentMode: StudentThemeMode;
  isSaving: boolean;
  onSelectPalette: (mode: StudentThemeMode, paletteId: string) => void;
  selectedAvatarSlug?: string | null;
  onSelectAvatar: (avatarSlug: string | null) => void;
};

export function StudentThemeSettingsPanel({
  open,
  onOpenChange,
  selectedLightPaletteId,
  selectedDarkPaletteId,
  currentMode,
  isSaving,
  onSelectPalette,
  selectedAvatarSlug,
  onSelectAvatar,
}: StudentThemeSettingsPanelProps) {
  const lightPalettes = getAvailableStudentThemePalettes('light');
  const darkPalettes = getAvailableStudentThemePalettes('dark');

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="left"
        className="w-full max-w-md border-[color:var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-text)] flex flex-col h-full p-0"
      >
        <SheetHeader className="gap-2 border-b border-[var(--admin-border)] p-6 shrink-0">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
              <Palette className="h-5 w-5" />
            </div>
            <div>
              <SheetTitle className="text-lg font-black text-[var(--admin-text)]">
                تخصيص الحساب
              </SheetTitle>
              <SheetDescription className="text-[var(--admin-muted)]">
                اختر الأفاتار الخاص بك وحدد ألوان ثيم المنصة.
              </SheetDescription>
              <p className="mt-2 inline-flex items-center rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
                الوضع النشط الآن: {currentMode === 'dark' ? 'داكن' : 'فاتح'}
              </p>
            </div>
          </div>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto p-6 space-y-8 scrollbar-thin">
          {/* Avatar Selection Section */}
          <section className="space-y-3">
            <h3 className="text-xs font-black tracking-[0.2em] text-[var(--admin-muted)] uppercase">
              شخصيتك الكارتونية (علماء ومفكرون)
            </h3>
            <div className="grid grid-cols-3 gap-3">
              {AVATAR_LIST.map((avatar) => {
                const isSelected = selectedAvatarSlug === avatar.slug;
                return (
                  <button
                    key={avatar.slug}
                    type="button"
                    onClick={() => onSelectAvatar(avatar.slug)}
                    disabled={isSaving}
                    className={cn(
                      'relative flex flex-col items-center gap-2 p-2 rounded-2xl border transition duration-300',
                      'border-[var(--admin-border)] bg-[var(--admin-card)] hover:bg-[var(--admin-card-strong)] hover:scale-105',
                      isSelected && 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] ring-2 ring-[var(--admin-primary)] shadow-[0_8px_20px_var(--admin-shadow)]'
                    )}
                  >
                    <div className="relative w-12 h-12 rounded-full overflow-hidden border border-[var(--admin-border)] bg-[var(--admin-bg)]">
                      <Image
                        src={avatar.imageUrl}
                        alt={avatar.name}
                        fill
                        sizes="48px"
                        className="object-cover"
                      />
                    </div>
                    <span className="text-[10px] font-black text-[var(--admin-text)] text-center truncate w-full">
                      {avatar.name}
                    </span>
                  </button>
                );
              })}
            </div>

            {/* Selected Avatar Detailed Info Box */}
            {selectedAvatarSlug && (
              <div className="mt-3 p-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-right flex gap-3 items-center shadow-inner">
                <div className="relative w-12 h-12 rounded-full overflow-hidden border border-[var(--admin-border)] bg-[var(--admin-bg)] shrink-0">
                  <Image
                    src={AVATAR_LIST.find(a => a.slug === selectedAvatarSlug)?.imageUrl || ''}
                    alt="Selected"
                    fill
                    sizes="48px"
                    className="object-cover"
                  />
                </div>
                <div className="space-y-0.5">
                  <h5 className="text-[12px] font-black text-[var(--admin-primary-strong)]">
                    {AVATAR_LIST.find(a => a.slug === selectedAvatarSlug)?.name}
                  </h5>
                  <p className="text-[10px] font-bold text-[var(--admin-muted)] leading-normal">
                    {AVATAR_LIST.find(a => a.slug === selectedAvatarSlug)?.info}
                  </p>
                </div>
              </div>
            )}
          </section>

          <PaletteSection
            title="ألوان الوضع الفاتح"
            palettes={lightPalettes}
            selectedPaletteId={selectedLightPaletteId}
            isSaving={isSaving}
            onSelect={(paletteId) => onSelectPalette('light', paletteId)}
          />

          <PaletteSection
            title="ألوان الوضع الداكن"
            palettes={darkPalettes}
            selectedPaletteId={selectedDarkPaletteId}
            isSaving={isSaving}
            onSelect={(paletteId) => onSelectPalette('dark', paletteId)}
          />
        </div>
      </SheetContent>
    </Sheet>
  );
}

function PaletteSection({
  title,
  palettes,
  selectedPaletteId,
  isSaving,
  onSelect,
}: {
  title: string;
  palettes: ReturnType<typeof getAvailableStudentThemePalettes>;
  selectedPaletteId: string;
  isSaving: boolean;
  onSelect: (paletteId: string) => void;
}) {
  return (
    <section className="space-y-3">
      <h3 className="pt-4 text-sm font-black tracking-[0.2em] text-[var(--admin-muted)]">
        {title}
      </h3>

      <div className="grid gap-3">
        {palettes.map((palette) => {
          const isSelected = palette.id === selectedPaletteId;

          return (
            <button
              key={palette.id}
              type="button"
              onClick={() => onSelect(palette.id)}
              disabled={isSaving}
              className={cn(
                'flex items-center justify-between rounded-[24px] border p-4 text-right transition',
                'border-[var(--admin-border)] bg-[var(--admin-card)] hover:bg-[var(--admin-card-strong)]',
                isSelected && 'border-[color:var(--admin-primary)] bg-[var(--admin-card-strong)] shadow-[0_12px_30px_var(--admin-shadow)]',
              )}
            >
              <div className="flex items-center gap-4">
                <div
                  className="h-12 w-12 rounded-[18px] border border-white/10 shadow-inner"
                  style={{
                    background: `linear-gradient(135deg, ${palette.previewAccent}, ${palette.tokens['--admin-primary-strong'] ?? palette.previewAccent})`,
                  }}
                />
                <div className="space-y-1">
                  <p className="font-black text-[var(--admin-text)]">{palette.name}</p>
                  <p className="text-xs text-[var(--admin-muted)]">
                    {palette.mode === 'light' ? 'مخصص للوضع الفاتح' : 'مخصص للوضع الداكن'}
                  </p>
                </div>
              </div>

              <div className="flex min-w-[3rem] justify-end">
                {isSaving && isSelected ? (
                  <Loader2 className="h-4 w-4 animate-spin text-[var(--admin-primary)]" />
                ) : isSelected ? (
                  <span className="flex items-center gap-1 rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
                    <Check className="h-3.5 w-3.5" />
                    مفعل
                  </span>
                ) : (
                  <span className="rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
                    اختيار
                  </span>
                )}
              </div>
            </button>
          );
        })}
      </div>
    </section>
  );
}

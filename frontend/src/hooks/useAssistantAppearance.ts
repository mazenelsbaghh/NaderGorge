'use client';

import { useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { setStoredAdminThemeMode } from '@/lib/admin-theme-mode';
import type { StudentThemeMode } from '@/lib/student-theme-palettes';
import {
  applyStudentThemeTokens, getDefaultStudentThemePalette, getResolvedStudentThemePalette, resetStudentThemeTokens,
} from '@/lib/student-theme-vars';
import apiClient from '@/services/api-client';

type AssistantAppearance = {
  currentMode: StudentThemeMode | null;
  selectedLightPaletteId: string;
  selectedDarkPaletteId: string;
};

const endpoint = '/assistant/theme-preferences';

export function useAssistantAppearance(accountId: string | undefined) {
  const adminTheme = useAdminTheme();
  const { mode } = adminTheme;
  const [snapshot, setSnapshot] = useState<{ accountId: string; appearance: AssistantAppearance } | null>(null);
  const [isLoadingPreferences, setLoading] = useState(true);
  const [isSavingPreferences, setSaving] = useState(false);
  const [preferencesError, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const accountRequest = useRef<AbortController | null>(null);
  const saving = useRef(false);

  useEffect(() => {
    if (!accountId) return;
    const controller = new AbortController();
    accountRequest.current = controller;
    saving.current = false;
    setSaving(false);
    setLoading(true);
    setError(null);

    apiClient.get<{ data: AssistantAppearance }>(endpoint, { signal: controller.signal, suppressErrorToast: true })
      .then(({ data: { data: appearance } }) => {
        if (controller.signal.aborted) return;
        setSnapshot({ accountId, appearance });
        if (appearance.currentMode) setStoredAdminThemeMode(appearance.currentMode);
      })
      .catch(() => {
        if (!controller.signal.aborted) setError('تعذر تحميل ألوان حسابك. حاول مرة أخرى.');
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });

    return () => controller.abort();
  }, [accountId, attempt]);

  const appearance = snapshot && snapshot.accountId === accountId ? snapshot.appearance : null;
  const selectedLightPaletteId = appearance?.selectedLightPaletteId ?? getDefaultStudentThemePalette('light').id;
  const selectedDarkPaletteId = appearance?.selectedDarkPaletteId ?? getDefaultStudentThemePalette('dark').id;
  const palette = appearance?.currentMode ? getResolvedStudentThemePalette(
    mode, mode === 'dark' ? selectedDarkPaletteId : selectedLightPaletteId,
  ) : null;

  useEffect(() => {
    if (!palette) return;
    applyStudentThemeTokens(palette.tokens);
    return resetStudentThemeTokens;
  }, [palette]);

  async function save(next: AssistantAppearance & { currentMode: StudentThemeMode }) {
    const signal = accountRequest.current?.signal;
    if (!accountId || !appearance || !signal || signal.aborted || saving.current) {
      throw new Error('انتظر تحميل إعدادات الألوان أو اكتمال الحفظ.');
    }

    saving.current = true;
    setSaving(true);
    setSnapshot({ accountId, appearance: next });
    if (next.currentMode !== mode) setStoredAdminThemeMode(next.currentMode);

    try {
      const { data } = await apiClient.put<{ data: AssistantAppearance }>(endpoint, {
        lightPaletteId: next.selectedLightPaletteId,
        darkPaletteId: next.selectedDarkPaletteId,
        currentMode: next.currentMode,
      }, { signal, suppressErrorToast: true });
      if (!signal.aborted) setSnapshot({ accountId, appearance: data.data });
    } catch (error) {
      if (!signal.aborted) {
        setSnapshot({ accountId, appearance });
        if (next.currentMode !== mode) setStoredAdminThemeMode(mode);
      }
      throw error;
    } finally {
      if (!signal.aborted) {
        saving.current = false;
        setSaving(false);
      }
    }
  }

  const updateMode = (currentMode: StudentThemeMode) => save({ currentMode, selectedLightPaletteId, selectedDarkPaletteId });

  return {
    mode,
    isDark: adminTheme.isDark,
    themeVars: { ...adminTheme.themeVars, ...palette?.tokens },
    toggleTheme: () => {
      if (isLoadingPreferences || !appearance) {
        adminTheme.toggleTheme();
      } else if (!saving.current) {
        void updateMode(mode === 'dark' ? 'light' : 'dark').catch(() => toast.error('تعذر حفظ وضع العرض. حاول مرة أخرى.'));
      }
    },
    updateMode,
    updatePalette: (paletteMode: StudentThemeMode, paletteId: string) => save({
      currentMode: mode,
      selectedLightPaletteId: paletteMode === 'light' ? paletteId : selectedLightPaletteId,
      selectedDarkPaletteId: paletteMode === 'dark' ? paletteId : selectedDarkPaletteId,
    }),
    selectedLightPaletteId,
    selectedDarkPaletteId,
    isReady: !!appearance && !isLoadingPreferences,
    isLoadingPreferences,
    isSavingPreferences,
    preferencesError,
    retryPreferences: () => setAttempt((value) => value + 1),
  };
}

'use client';

import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useSyncExternalStore,
  type ReactNode,
} from 'react';

import {
  getAdminThemeModeServerSnapshot,
  getStoredAdminThemeMode,
  setStoredAdminThemeMode,
  subscribeToAdminThemeMode,
  type AdminThemeMode,
} from '@/lib/admin-theme-mode';
import {
  applyStudentThemeTokens,
  getDefaultStudentThemePalette,
  getResolvedStudentThemePalette,
  resetStudentThemeTokens,
} from '@/lib/student-theme-vars';
import { useStudentThemePreferences } from '@/hooks/useStudentThemePreferences';
import { studentThemePalettes, type StudentThemeMode } from '@/lib/student-theme-palettes';
import { useAuthStore } from '@/stores/auth-store';

type StudentThemeContextValue = {
  mode: AdminThemeMode;
  isDark: boolean;
  toggleTheme: () => void;
  isReady: boolean;
  isLoadingPreferences: boolean;
  isSavingPreferences: boolean;
  selectedLightPaletteId: string;
  selectedDarkPaletteId: string;
  currentPaletteAccent: string;
  updatePalette: (mode: StudentThemeMode, paletteId: string) => Promise<void>;
  updateAvatar: (avatarSlug: string | null) => Promise<void>;
};

const StudentThemeContext = createContext<StudentThemeContextValue | null>(null);

export function StudentThemeProvider({ children }: { children: ReactNode }) {
  const mode = useSyncExternalStore(
    subscribeToAdminThemeMode,
    getStoredAdminThemeMode,
    getAdminThemeModeServerSnapshot,
  );
  const { preferences, isLoading, isSaving, updatePaletteForMode, updateCurrentMode, updatePreferences } = useStudentThemePreferences();
  const hasSyncedInitialMode = useRef(false);

  const selectedLightPaletteId = preferences?.selectedLightPaletteId ?? getDefaultStudentThemePalette('light').id;
  const selectedDarkPaletteId = preferences?.selectedDarkPaletteId ?? getDefaultStudentThemePalette('dark').id;

  const currentPalette = useMemo(
    () => getResolvedStudentThemePalette(mode, mode === 'dark' ? selectedDarkPaletteId : selectedLightPaletteId),
    [mode, selectedDarkPaletteId, selectedLightPaletteId],
  );

  useEffect(() => {
    if (!preferences || hasSyncedInitialMode.current) {
      return;
    }

    hasSyncedInitialMode.current = true;
    if (preferences.currentMode !== mode) {
      setStoredAdminThemeMode(preferences.currentMode);
    }
  }, [mode, preferences]);

  useEffect(() => {
    if (typeof document !== 'undefined') {
      document.documentElement.dataset.studentThemeSurface = 'student';
      document.documentElement.dataset.studentThemePalette = currentPalette.id;
    }

    applyStudentThemeTokens(currentPalette.tokens);

    return () => {
      if (typeof document !== 'undefined') {
        delete document.documentElement.dataset.studentThemeSurface;
        delete document.documentElement.dataset.studentThemePalette;
      }
      resetStudentThemeTokens();
    };
  }, [currentPalette]);

  const value = useMemo<StudentThemeContextValue>(() => ({
    mode,
    isDark: mode === 'dark',
    toggleTheme: () => {
      const nextMode = mode === 'dark' ? 'light' : 'dark';
      setStoredAdminThemeMode(nextMode);
      void updateCurrentMode(nextMode, {
        lightPaletteId: selectedLightPaletteId,
        darkPaletteId: selectedDarkPaletteId,
      });
    },
    isReady: !isLoading,
    isLoadingPreferences: isLoading,
    isSavingPreferences: isSaving,
    selectedLightPaletteId,
    selectedDarkPaletteId,
    currentPaletteAccent: currentPalette.previewAccent,
    updatePalette: async (paletteMode, paletteId) => {
      await updatePaletteForMode(paletteMode, paletteId, {
        lightPaletteId: selectedLightPaletteId,
        darkPaletteId: selectedDarkPaletteId,
      }, mode === 'dark' ? 'dark' : 'light');
    },
    updateAvatar: async (avatarSlug) => {
      await updatePreferences({
        lightPaletteId: selectedLightPaletteId,
        darkPaletteId: selectedDarkPaletteId,
        currentMode: mode === 'dark' ? 'dark' : 'light',
        avatarSlug,
      });
      useAuthStore.getState().updateAvatar(avatarSlug);
    },
  }), [
    currentPalette.previewAccent,
    isLoading,
    isSaving,
    mode,
    selectedDarkPaletteId,
    selectedLightPaletteId,
    updateCurrentMode,
    updatePaletteForMode,
    updatePreferences,
  ]);

  return (
    <StudentThemeContext.Provider value={value}>
      {children}
    </StudentThemeContext.Provider>
  );
}

export function useStudentTheme() {
  const context = useContext(StudentThemeContext);

  if (!context) {
    throw new Error('useStudentTheme must be used within StudentThemeProvider');
  }

  return context;
}

export function getAvailableStudentThemePalettes(mode: StudentThemeMode) {
  return studentThemePalettes.filter((palette) => palette.mode === mode && palette.status === 'active');
}

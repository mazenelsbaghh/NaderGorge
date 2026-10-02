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
import toast from 'react-hot-toast';
import { useAuthStore } from '@/stores/auth-store';

type StudentThemeContextValue = {
  mode: AdminThemeMode;
  isDark: boolean;
  toggleTheme: () => void;
  updateMode: (mode: StudentThemeMode) => Promise<void>;
  preferencesError: string | null;
  retryPreferences: () => void;
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
  const { preferences, isLoading, isSaving, updatePaletteForMode, updateCurrentMode, updatePreferences, loadError, retryPreferences } = useStudentThemePreferences();
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
    if (!preferences) {
      return;
    }

    const persistedAvatar = preferences.avatarSlug ?? null;
    const currentAvatar = useAuthStore.getState().user?.avatarSlug ?? null;
    if (persistedAvatar !== currentAvatar) {
      useAuthStore.getState().updateAvatar(persistedAvatar);
    }
  }, [preferences]);

  useEffect(() => {
    if (typeof document !== 'undefined') {
      document.documentElement.classList.toggle('dark', mode === 'dark');
      document.documentElement.dataset.themeMode = mode;
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
  }, [currentPalette, mode]);

  const updateMode = async (nextMode: StudentThemeMode) => {
    if (isLoading || isSaving || !preferences || nextMode === mode) return;
    setStoredAdminThemeMode(nextMode);
    try {
      await updateCurrentMode(nextMode, {
        lightPaletteId: selectedLightPaletteId,
        darkPaletteId: selectedDarkPaletteId,
      });
    } catch (error) {
      setStoredAdminThemeMode(mode);
      throw error;
    }
  };

  const value: StudentThemeContextValue = {
    mode,
    isDark: mode === 'dark',
    toggleTheme: () => {
      if (isLoading || isSaving || !preferences) return;
      void updateMode(mode === 'dark' ? 'light' : 'dark').catch(() => {
        toast.error('تعذر حفظ وضع العرض. حاول مرة أخرى.');
      });
    },
    updateMode,
    preferencesError: loadError,
    retryPreferences,
    isReady: !isLoading && !!preferences,
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
    },
  };

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

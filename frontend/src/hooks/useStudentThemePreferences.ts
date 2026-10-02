'use client';

import { useEffect, useRef, useState } from 'react';

import { isStudentPaletteAllowedForMode } from '@/lib/student-theme-vars';
import { type StudentThemeMode } from '@/lib/student-theme-palettes';
import { studentService, type StudentThemePreferencesDto } from '@/services/student-service';

type UpdateThemePreferencesPayload = {
  lightPaletteId: string;
  darkPaletteId: string;
  currentMode: StudentThemeMode;
  avatarSlug?: string | null;
};

export function useStudentThemePreferences() {
  const [preferences, setPreferences] = useState<StudentThemePreferencesDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loadAttempt, setLoadAttempt] = useState(0);
  const saveInFlight = useRef(false);

  useEffect(() => {
    let isActive = true;

    studentService.getThemePreferences()
      .then((data) => {
        if (!isActive) return;
        setPreferences(data);
      })
      .catch(() => {
        if (!isActive) return;
        setLoadError('تعذر تحميل ألوان حسابك. حاول مرة أخرى.');
      })
      .finally(() => {
        if (!isActive) return;
        setIsLoading(false);
      });

    return () => {
      isActive = false;
    };
  }, [loadAttempt]);

  const updatePreferences = async (payload: UpdateThemePreferencesPayload) => {
    if (!preferences || saveInFlight.current) {
      throw new Error('انتظر تحميل الإعدادات أو اكتمال الحفظ.');
    }

    saveInFlight.current = true;
    setIsSaving(true);
    const previousPreferences = preferences;
    setPreferences({
      ...preferences,
      selectedLightPaletteId: payload.lightPaletteId,
      selectedDarkPaletteId: payload.darkPaletteId,
      currentMode: payload.currentMode,
      avatarSlug: payload.avatarSlug ?? preferences.avatarSlug,
    });

    try {
      const next = await studentService.updateThemePreferences(payload);
      setPreferences(next);
      return next;
    } catch (error) {
      setPreferences(previousPreferences);
      throw error;
    } finally {
      saveInFlight.current = false;
      setIsSaving(false);
    }
  };

  const updatePaletteForMode = async (
    paletteMode: StudentThemeMode,
    paletteId: string,
    currentSelections: { lightPaletteId: string; darkPaletteId: string; },
    currentMode: StudentThemeMode,
  ) => {
    if (!isStudentPaletteAllowedForMode(paletteMode, paletteId)) {
      return preferences;
    }

    return updatePreferences({
      lightPaletteId: paletteMode === 'light' ? paletteId : currentSelections.lightPaletteId,
      darkPaletteId: paletteMode === 'dark' ? paletteId : currentSelections.darkPaletteId,
      currentMode,
    });
  };

  const updateCurrentMode = async (
    currentMode: StudentThemeMode,
    currentSelections: { lightPaletteId: string; darkPaletteId: string; },
  ) => updatePreferences({
    lightPaletteId: currentSelections.lightPaletteId,
    darkPaletteId: currentSelections.darkPaletteId,
    currentMode,
  });

  return {
    preferences,
    isLoading,
    isSaving,
    loadError,
    retryPreferences: () => {
      setLoadError(null);
      setIsLoading(true);
      setLoadAttempt((attempt) => attempt + 1);
    },
    updatePreferences,
    updatePaletteForMode,
    updateCurrentMode,
  };
}

'use client';

import { useEffect, useState } from 'react';

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

  useEffect(() => {
    let isActive = true;

    studentService.getThemePreferences()
      .then((data) => {
        if (!isActive) return;
        setPreferences(data);
      })
      .catch(() => {
        if (!isActive) return;
        setPreferences(null);
      })
      .finally(() => {
        if (!isActive) return;
        setIsLoading(false);
      });

    return () => {
      isActive = false;
    };
  }, []);

  const updatePreferences = async (payload: UpdateThemePreferencesPayload) => {
    setIsSaving(true);

    try {
      const next = await studentService.updateThemePreferences(payload);
      setPreferences(next);
      return next;
    } finally {
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
    updatePreferences,
    updatePaletteForMode,
    updateCurrentMode,
  };
}

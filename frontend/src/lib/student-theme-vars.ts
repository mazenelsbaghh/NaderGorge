'use client';

import { studentThemePalettes, type StudentThemeMode, type StudentThemePalette, type StudentThemeTokens } from '@/lib/student-theme-palettes';

const THEME_VAR_KEYS = [
  '--admin-bg',
  '--admin-bg-overlay',
  '--admin-dot',
  '--admin-sidebar',
  '--admin-text',
  '--admin-muted',
  '--admin-primary',
  '--admin-primary-strong',
  '--admin-primary-contrast',
  '--admin-hover',
  '--admin-card',
  '--admin-card-soft',
  '--admin-card-strong',
  '--admin-border',
  '--admin-search',
  '--admin-footer',
  '--admin-shadow',
  '--admin-primary-15',
] as const;

export function getStudentThemePaletteById(paletteId: string): StudentThemePalette | undefined {
  return studentThemePalettes.find((palette) => palette.id === paletteId && palette.status === 'active');
}

export function getStudentThemePalettesByMode(mode: StudentThemeMode): StudentThemePalette[] {
  return studentThemePalettes.filter((palette) => palette.mode === mode && palette.status === 'active');
}

export function getDefaultStudentThemePalette(mode: StudentThemeMode): StudentThemePalette {
  const fallbackId = mode === 'dark' ? 'massar-dark' : 'massar-light';
  return getStudentThemePaletteById(fallbackId) ?? studentThemePalettes[0];
}

export function getResolvedStudentThemePalette(mode: StudentThemeMode, paletteId?: string): StudentThemePalette {
  const palette = paletteId ? getStudentThemePaletteById(paletteId) : undefined;
  if (palette && palette.mode === mode) {
    return palette;
  }

  return getDefaultStudentThemePalette(mode);
}

export function getModeScopedStudentPaletteIds(mode: StudentThemeMode): string[] {
  return getStudentThemePalettesByMode(mode).map((palette) => palette.id);
}

export function isStudentPaletteAllowedForMode(mode: StudentThemeMode, paletteId: string): boolean {
  return getModeScopedStudentPaletteIds(mode).includes(paletteId);
}

export function applyStudentThemeTokens(tokens: StudentThemeTokens) {
  if (typeof document === 'undefined') return;

  for (const [key, value] of Object.entries(tokens)) {
    document.documentElement.style.setProperty(key, value);
    document.body.style.setProperty(key, value);
  }
}

export function resetStudentThemeTokens() {
  if (typeof document === 'undefined') return;

  for (const key of THEME_VAR_KEYS) {
    document.documentElement.style.removeProperty(key);
    document.body.style.removeProperty(key);
  }
}

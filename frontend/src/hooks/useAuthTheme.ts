'use client';

/**
 * useAuthTheme — mirrors useAdminTheme.ts exactly.
 *
 * Reads / writes the same localStorage key ("admin-theme-mode") so the
 * chosen mode persists across the Admin shell and the Auth pages.
 *
 * Token values are copy-pasted from useAdminTheme.ts (dark & light maps)
 * so that both surfaces stay visually in sync without importing Admin
 * components into the public route group.
 */

import { CSSProperties, useEffect, useMemo, useSyncExternalStore } from 'react';

import {
  getAdminThemeModeServerSnapshot,
  getStoredAdminThemeMode,
  setStoredAdminThemeMode,
  subscribeToAdminThemeMode,
  type AdminThemeMode,
} from '@/lib/admin-theme-mode';

type ThemeMode = AdminThemeMode;

function buildVars(mode: ThemeMode): CSSProperties {
  // ── DARK MODE — same values as useAdminTheme.ts dark branch ──────────
  if (mode === 'dark') {
    return {
      ['--admin-bg' as string]: '#1a1a19',
      ['--admin-bg-overlay' as string]: 'rgba(12,12,12,0.95)',
      ['--admin-dot' as string]: '#775a19',
      ['--admin-sidebar' as string]: 'rgba(26,26,24,0.82)',
      ['--admin-text' as string]: '#f4f1e7',
      ['--admin-muted' as string]: '#d1c5b4',
      ['--admin-primary' as string]: '#c5a059',
      ['--admin-primary-strong' as string]: '#8f6b2f',
      ['--admin-primary-contrast' as string]: '#0c0c0c',
      ['--admin-hover' as string]: '#2c261f',
      ['--admin-card' as string]: 'rgba(30,30,30,0.85)',
      ['--admin-card-soft' as string]: 'rgba(21,21,18,0.95)',
      ['--admin-card-strong' as string]: 'rgba(40,40,36,0.95)',
      ['--admin-border' as string]: 'rgba(119,90,25,0.2)',
      ['--admin-search' as string]: 'rgba(12,12,12,0.85)',
      ['--admin-footer' as string]: '#c5a059',
      ['--admin-shadow' as string]: 'rgba(0,0,0,0.5)',
      ['--admin-danger' as string]: '#ef4444',
    };
  }

  // ── LIGHT MODE — same values as useAdminTheme.ts light branch ────────
  return {
    ['--admin-bg' as string]: '#fcf9ef',
    ['--admin-bg-overlay' as string]: 'rgba(252,249,239,0.9)',
    ['--admin-dot' as string]: 'rgba(209,197,180,0.8)',
    ['--admin-sidebar' as string]: '#f1eee4',
    ['--admin-text' as string]: '#1c1c16',
    ['--admin-muted' as string]: '#7f7667',
    ['--admin-primary' as string]: '#5d4300',
    ['--admin-primary-strong' as string]: '#775a19',
    ['--admin-primary-contrast' as string]: '#fcd386',
    ['--admin-hover' as string]: '#e5e2d9',
    ['--admin-card' as string]: 'rgba(241,238,228,0.95)',
    ['--admin-card-soft' as string]: 'rgba(246,244,234,0.96)',
    ['--admin-card-strong' as string]: 'rgba(235,232,222,0.96)',
    ['--admin-border' as string]: 'rgba(255,255,255,0.3)',
    ['--admin-search' as string]: 'rgba(255,255,255,0.7)',
    ['--admin-footer' as string]: '#e8c176',
    ['--admin-shadow' as string]: 'rgba(78,70,57,0.1)',
    ['--admin-danger' as string]: '#ef4444',
  };
}

export function useAuthTheme() {
  const mode = useSyncExternalStore(
    subscribeToAdminThemeMode,
    getStoredAdminThemeMode,
    getAdminThemeModeServerSnapshot,
  );

  useEffect(() => {
    if (typeof document === 'undefined') return;
    document.documentElement.classList.toggle('dark', mode === 'dark');
    document.documentElement.dataset.themeMode = mode;
  }, [mode]);

  const themeVars = useMemo(() => buildVars(mode), [mode]);

  return {
    mode,
    isDark: mode === 'dark',
    themeVars,
    toggleTheme: () => setStoredAdminThemeMode(mode === 'dark' ? 'light' : 'dark'),
  };
}

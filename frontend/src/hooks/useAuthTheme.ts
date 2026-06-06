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

import { CSSProperties, useEffect, useMemo, useState, useSyncExternalStore } from 'react';

import {
  getAdminThemeModeServerSnapshot,
  getStoredAdminThemeMode,
  setStoredAdminThemeMode,
  subscribeToAdminThemeMode,
  type AdminThemeMode,
} from '@/lib/admin-theme-mode';

type ThemeMode = AdminThemeMode;

function buildVars(mode: ThemeMode): CSSProperties {
  // ── DARK MODE — Rainy Night ──────────
  if (mode === 'dark') {
    return {
      ['--admin-bg' as string]: '#0f172a',
      ['--admin-bg-overlay' as string]: 'rgba(15,23,42,0.95)',
      ['--admin-dot' as string]: '#334155',
      ['--admin-sidebar' as string]: 'rgba(15,23,42,0.84)',
      ['--admin-text' as string]: '#f8fafc',
      ['--admin-muted' as string]: '#94a3b8',
      ['--admin-primary' as string]: '#64748b',
      ['--admin-primary-strong' as string]: '#475569',
      ['--admin-primary-contrast' as string]: '#f8fbff',
      ['--admin-hover' as string]: '#1e293b',
      ['--admin-card' as string]: 'rgba(30,41,59,0.88)',
      ['--admin-card-soft' as string]: 'rgba(15,23,42,0.96)',
      ['--admin-card-strong' as string]: 'rgba(51,65,85,0.96)',
      ['--admin-border' as string]: 'rgba(100,116,139,0.16)',
      ['--admin-search' as string]: 'rgba(15,23,42,0.88)',
      ['--admin-footer' as string]: '#64748b',
      ['--admin-shadow' as string]: 'rgba(3,7,14,0.5)',
      ['--admin-primary-15' as string]: 'rgba(100,116,139,0.15)',
      ['--admin-danger' as string]: '#ef4444',
    };
  }

  // ── LIGHT MODE — Winter Sky ────────
  return {
    ['--admin-bg' as string]: '#f8fafc',
    ['--admin-bg-overlay' as string]: 'rgba(248,250,252,0.92)',
    ['--admin-dot' as string]: 'rgba(100,116,139,0.2)',
    ['--admin-sidebar' as string]: '#f1f5f9',
    ['--admin-text' as string]: '#0f172a',
    ['--admin-muted' as string]: '#64748b',
    ['--admin-primary' as string]: '#475569',
    ['--admin-primary-strong' as string]: '#334155',
    ['--admin-primary-contrast' as string]: '#f8fbff',
    ['--admin-hover' as string]: '#e2e8f0',
    ['--admin-card' as string]: 'rgba(241,245,249,0.95)',
    ['--admin-card-soft' as string]: 'rgba(248,250,252,0.98)',
    ['--admin-card-strong' as string]: 'rgba(226,232,240,0.96)',
    ['--admin-border' as string]: 'rgba(71,85,105,0.12)',
    ['--admin-search' as string]: 'rgba(248,250,252,0.84)',
    ['--admin-footer' as string]: '#64748b',
    ['--admin-shadow' as string]: 'rgba(71,85,105,0.1)',
    ['--admin-primary-15' as string]: 'rgba(71,85,105,0.12)',
    ['--admin-danger' as string]: '#ef4444',
  };
}

export function useAuthTheme() {
  const [isMounted, setIsMounted] = useState(false);
  const rawMode = useSyncExternalStore(
    subscribeToAdminThemeMode,
    getStoredAdminThemeMode,
    getAdminThemeModeServerSnapshot,
  );

  useEffect(() => {
    setIsMounted(true);
  }, []);

  const mode = isMounted ? rawMode : 'light';

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

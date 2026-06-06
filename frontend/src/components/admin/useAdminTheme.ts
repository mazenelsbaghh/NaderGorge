'use client';

import { CSSProperties, useEffect, useMemo, useState, useSyncExternalStore } from 'react';

import {
  getAdminThemeModeServerSnapshot,
  getStoredAdminThemeMode,
  setStoredAdminThemeMode,
  subscribeToAdminThemeMode,
  type AdminThemeMode,
} from '@/lib/admin-theme-mode';

function getThemeVars(mode: AdminThemeMode): CSSProperties {
  if (mode === 'dark') {
    return {
      ['--admin-bg' as string]: '#0f172a',
      ['--admin-bg-overlay' as string]: 'rgba(15,23,42,0.94)',
      ['--admin-dot' as string]: '#334155',
      ['--admin-sidebar' as string]: 'rgba(15,23,42,0.86)',
      ['--admin-text' as string]: '#e2e8f0',
      ['--admin-muted' as string]: '#94a3b8',
      ['--admin-primary' as string]: '#64748b',
      ['--admin-primary-strong' as string]: '#475569',
      ['--admin-primary-contrast' as string]: '#f8fafc',
      ['--admin-hover' as string]: 'rgba(51,65,85,0.72)',
      ['--admin-card' as string]: 'rgba(30,41,59,0.86)',
      ['--admin-card-soft' as string]: 'rgba(15,23,42,0.92)',
      ['--admin-card-strong' as string]: 'rgba(30,41,59,0.95)',
      ['--admin-border' as string]: 'rgba(148,163,184,0.22)',
      ['--admin-search' as string]: 'rgba(15,23,42,0.84)',
      ['--admin-footer' as string]: '#94a3b8',
      ['--admin-shadow' as string]: 'rgba(2,6,23,0.52)',
      ['--admin-primary-15' as string]: 'rgba(100,116,139,0.18)',
    };
  }

  return {
    ['--admin-bg' as string]: '#f8fafc',
    ['--admin-bg-overlay' as string]: 'rgba(248,250,252,0.92)',
    ['--admin-dot' as string]: 'rgba(100,116,139,0.2)',
    ['--admin-sidebar' as string]: '#eef4f8',
    ['--admin-text' as string]: '#0f172a',
    ['--admin-muted' as string]: '#475569',
    ['--admin-primary' as string]: '#475569',
    ['--admin-primary-strong' as string]: '#334155',
    ['--admin-primary-contrast' as string]: '#f8fafc',
    ['--admin-hover' as string]: '#e2e8f0',
    ['--admin-card' as string]: 'rgba(255,255,255,0.92)',
    ['--admin-card-soft' as string]: 'rgba(248,250,252,0.96)',
    ['--admin-card-strong' as string]: 'rgba(241,245,249,0.96)',
    ['--admin-border' as string]: 'rgba(100,116,139,0.18)',
    ['--admin-search' as string]: 'rgba(248,250,252,0.82)',
    ['--admin-footer' as string]: '#64748b',
    ['--admin-shadow' as string]: 'rgba(15,23,42,0.12)',
    ['--admin-primary-15' as string]: 'rgba(71,85,105,0.14)',
  };
}

export function useAdminTheme() {
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

  const themeVars = useMemo(() => getThemeVars(mode), [mode]);

  return {
    mode,
    isDark: mode === 'dark',
    themeVars,
    toggleTheme: () => setStoredAdminThemeMode(mode === 'dark' ? 'light' : 'dark'),
  };
}

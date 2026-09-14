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
      ['--admin-bg' as string]: '#111115',
      ['--admin-bg-overlay' as string]: 'rgba(12,12,15,0.95)',
      ['--admin-dot' as string]: 'rgba(14,143,143,0.22)',
      ['--admin-sidebar' as string]: 'rgba(21,21,25,0.94)',
      ['--admin-text' as string]: '#f4f1e7',
      ['--admin-muted' as string]: '#d1c5b4',
      ['--admin-primary' as string]: '#0E8F8F',
      ['--admin-primary-strong' as string]: '#0A1D3D',
      ['--admin-primary-contrast' as string]: '#ffffff',
      ['--admin-hover' as string]: 'rgba(14,143,143,0.14)',
      ['--admin-card' as string]: 'rgba(30,30,35,0.85)',
      ['--admin-card-soft' as string]: 'rgba(21,21,25,0.95)',
      ['--admin-card-strong' as string]: 'rgba(40,40,46,0.95)',
      ['--admin-border' as string]: 'rgba(14,143,143,0.2)',
      ['--admin-search' as string]: 'rgba(21,21,25,0.92)',
      ['--admin-footer' as string]: '#d1c5b4',
      ['--admin-shadow' as string]: 'rgba(0,0,0,0.42)',
      ['--admin-primary-15' as string]: 'rgba(14,143,143,0.15)',
      ['--admin-success' as string]: 'oklch(0.62 0.15 145)',
      ['--admin-success-10' as string]: 'oklch(0.62 0.15 145 / 10%)',
      ['--admin-success-20' as string]: 'oklch(0.62 0.15 145 / 20%)',
      ['--admin-danger' as string]: '#ef4444',
      ['--admin-danger-10' as string]: 'oklch(0.65 0.2 25 / 10%)',
      ['--admin-danger-20' as string]: 'oklch(0.65 0.2 25 / 20%)',
      ['--admin-warning' as string]: 'oklch(0.7 0.15 50)',
      ['--admin-warning-10' as string]: 'oklch(0.7 0.15 50 / 10%)',
      ['--admin-warning-20' as string]: 'oklch(0.7 0.15 50 / 20%)',
    };
  }

  return {
    ['--admin-bg' as string]: '#f5f8fb',
    ['--admin-bg-overlay' as string]: 'rgba(245,248,251,0.94)',
    ['--admin-dot' as string]: 'rgba(10,29,61,0.16)',
    ['--admin-sidebar' as string]: '#eef5f7',
    ['--admin-text' as string]: '#0A1D3D',
    ['--admin-muted' as string]: '#2E3A47',
    ['--admin-primary' as string]: '#0A1D3D',
    ['--admin-primary-strong' as string]: '#021f45',
    ['--admin-primary-contrast' as string]: '#ffffff',
    ['--admin-hover' as string]: '#e5eef2',
    ['--admin-card' as string]: 'rgba(255,255,255,0.97)',
    ['--admin-card-soft' as string]: '#f1f6f8',
    ['--admin-card-strong' as string]: '#e5eef2',
    ['--admin-border' as string]: 'rgba(10,29,61,0.15)',
    ['--admin-search' as string]: 'rgba(255,255,255,0.92)',
    ['--admin-footer' as string]: '#2E3A47',
    ['--admin-shadow' as string]: 'rgba(10,29,61,0.12)',
    ['--admin-primary-15' as string]: 'rgba(10,29,61,0.12)',
    ['--admin-success' as string]: '#16a34a',
    ['--admin-success-10' as string]: 'rgba(22,163,74,0.1)',
    ['--admin-success-20' as string]: 'rgba(22,163,74,0.18)',
    ['--admin-danger' as string]: '#dc2626',
    ['--admin-danger-10' as string]: 'rgba(220,38,38,0.1)',
    ['--admin-danger-20' as string]: 'rgba(220,38,38,0.18)',
    ['--admin-warning' as string]: '#c2410c',
    ['--admin-warning-10' as string]: 'rgba(194,65,12,0.1)',
    ['--admin-warning-20' as string]: 'rgba(194,65,12,0.18)',
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

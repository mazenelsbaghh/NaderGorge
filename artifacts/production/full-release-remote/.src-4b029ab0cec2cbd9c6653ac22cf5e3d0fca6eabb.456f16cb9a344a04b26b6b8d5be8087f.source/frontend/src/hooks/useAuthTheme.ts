'use client';

/**
 * useAuthTheme mirrors the global Massar Academy admin tokens on public auth
 * routes while keeping the persisted admin theme mode.
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
      ['--admin-danger' as string]: '#ef4444',
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
    ['--admin-footer' as string]: '#0E8F8F',
    ['--admin-shadow' as string]: 'rgba(10,29,61,0.12)',
    ['--admin-primary-15' as string]: 'rgba(10,29,61,0.12)',
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

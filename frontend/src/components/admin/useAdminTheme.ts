'use client';

import { CSSProperties, useEffect, useMemo, useState } from 'react';

const STORAGE_KEY = 'admin-theme-mode';

type AdminThemeMode = 'light' | 'dark';

function getThemeVars(mode: AdminThemeMode): CSSProperties {
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
      ['--admin-card' as string]: 'rgba(30,30,30,0.45)',
      ['--admin-card-soft' as string]: 'rgba(21,21,18,0.9)',
      ['--admin-card-strong' as string]: 'rgba(40,40,36,0.9)',
      ['--admin-border' as string]: 'rgba(119,90,25,0.2)',
      ['--admin-search' as string]: 'rgba(12,12,12,0.85)',
      ['--admin-footer' as string]: '#c5a059',
      ['--admin-shadow' as string]: 'rgba(0,0,0,0.5)',
      ['--admin-primary-15' as string]: 'rgba(197,160,89,0.15)',
    };
  }

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
    ['--admin-card' as string]: 'rgba(241,238,228,0.65)',
    ['--admin-card-soft' as string]: 'rgba(246,244,234,0.82)',
    ['--admin-card-strong' as string]: 'rgba(235,232,222,0.82)',
    ['--admin-border' as string]: 'rgba(255,255,255,0.3)',
    ['--admin-search' as string]: 'rgba(255,255,255,0.7)',
    ['--admin-footer' as string]: '#e8c176',
    ['--admin-shadow' as string]: 'rgba(78,70,57,0.1)',
    ['--admin-primary-15' as string]: 'rgba(93,67,0,0.12)',
  };
}

export function useAdminTheme() {
  const [mode, setMode] = useState<AdminThemeMode>(() => {
    if (typeof window === 'undefined') return 'light';
    const saved = window.localStorage.getItem(STORAGE_KEY);
    return saved === 'dark' || saved === 'light' ? saved : 'light';
  });

  useEffect(() => {
    if (typeof window === 'undefined') return;
    window.localStorage.setItem(STORAGE_KEY, mode);
  }, [mode]);

  const themeVars = useMemo(() => getThemeVars(mode), [mode]);

  return {
    mode,
    isDark: mode === 'dark',
    themeVars,
    toggleTheme: () => setMode((current) => (current === 'dark' ? 'light' : 'dark')),
  };
}

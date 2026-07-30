'use client';

export const ADMIN_THEME_MODE_STORAGE_KEY = 'admin-theme-mode';

const THEME_MODE_EVENT = 'admin-theme-mode-change';

export type AdminThemeMode = 'light' | 'dark';

export function getAdminThemeModeServerSnapshot(): AdminThemeMode {
  if (typeof document !== 'undefined') {
    const documentMode = document.documentElement.dataset.themeMode;
    if (documentMode === 'dark' || documentMode === 'light') {
      return documentMode;
    }

    return document.documentElement.classList.contains('dark') ? 'dark' : 'light';
  }

  return 'light';
}

export function getStoredAdminThemeMode(): AdminThemeMode {
  if (typeof window === 'undefined') {
    return getAdminThemeModeServerSnapshot();
  }

  const saved = window.localStorage.getItem(ADMIN_THEME_MODE_STORAGE_KEY);
  return saved === 'dark' || saved === 'light' ? saved : 'light';
}

export function setStoredAdminThemeMode(mode: AdminThemeMode) {
  if (typeof window === 'undefined') return;

  window.localStorage.setItem(ADMIN_THEME_MODE_STORAGE_KEY, mode);
  window.dispatchEvent(new Event(THEME_MODE_EVENT));
}

export function subscribeToAdminThemeMode(onStoreChange: () => void) {
  if (typeof window === 'undefined') {
    return () => {};
  }

  const handleStorage = (event: StorageEvent) => {
    if (event.key === ADMIN_THEME_MODE_STORAGE_KEY) {
      onStoreChange();
    }
  };

  window.addEventListener('storage', handleStorage);
  window.addEventListener(THEME_MODE_EVENT, onStoreChange);

  return () => {
    window.removeEventListener('storage', handleStorage);
    window.removeEventListener(THEME_MODE_EVENT, onStoreChange);
  };
}

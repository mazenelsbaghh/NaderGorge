'use client';

import { useEffect } from 'react';

import { useAuthStore } from '@/stores/auth-store';
import { useWebVitalsReporter } from '@/hooks/useWebVitalsReporter';
import { useCurrentSession } from '@/hooks/useCurrentSession';

export function AuthBootstrap() {
  const loadFromStorage = useAuthStore((state) => state.loadFromStorage);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const { refresh } = useCurrentSession();
  useWebVitalsReporter();

  useEffect(() => {
    loadFromStorage();
  }, [loadFromStorage]);

  useEffect(() => {
    if (isAuthenticated) {
      void refresh();
    }
  }, [isAuthenticated, refresh]);

  return null;
}

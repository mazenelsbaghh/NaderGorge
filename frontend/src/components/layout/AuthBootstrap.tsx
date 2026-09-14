'use client';

import { useEffect } from 'react';

import { useAuthStore } from '@/stores/auth-store';
import { useCurrentSession } from '@/hooks/useCurrentSession';

export function AuthBootstrap() {
  const loadFromStorage = useAuthStore((state) => state.loadFromStorage);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const { refresh } = useCurrentSession();
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

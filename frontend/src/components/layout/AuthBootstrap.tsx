'use client';

import { useEffect } from 'react';

import { useAuthStore } from '@/stores/auth-store';
import { useWebVitalsReporter } from '@/hooks/useWebVitalsReporter';

export function AuthBootstrap() {
  const loadFromStorage = useAuthStore((state) => state.loadFromStorage);
  useWebVitalsReporter();

  useEffect(() => {
    loadFromStorage();
  }, [loadFromStorage]);

  return null;
}

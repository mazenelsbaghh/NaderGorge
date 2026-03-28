'use client';

import { useEffect } from 'react';

import { useAuthStore } from '@/stores/auth-store';

export function AuthBootstrap() {
  const loadFromStorage = useAuthStore((state) => state.loadFromStorage);

  useEffect(() => {
    loadFromStorage();
  }, [loadFromStorage]);

  return null;
}

'use client';

import { useCallback, useState } from 'react';

import { useAuthStore } from '@/stores/auth-store';

export function useCurrentSession() {
  const refreshCurrentSession = useAuthStore((state) => state.refreshCurrentSession);
  const [isRefreshing, setIsRefreshing] = useState(false);

  const refresh = useCallback(async () => {
    setIsRefreshing(true);
    try {
      await refreshCurrentSession();
    } finally {
      setIsRefreshing(false);
    }
  }, [refreshCurrentSession]);

  return { refresh, isRefreshing };
}

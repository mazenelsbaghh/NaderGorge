'use client';

import { useCallback, useEffect, useState } from 'react';
import { adminService, type VideoTypeDto } from '@/services/admin-service';

export function useVideoTypes(includeInactive = false) {
  const [types, setTypes] = useState<VideoTypeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      const result = await adminService.listVideoTypes(includeInactive);
      if (!signal?.aborted) setTypes(result);
    } catch {
      if (!signal?.aborted) {
        setTypes([]);
        setError('تعذر تحميل أنواع الفيديو.');
      }
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, [includeInactive]);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  const retry = useCallback(() => load(), [load]);

  return { types, loading, error, retry };
}

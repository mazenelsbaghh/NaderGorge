'use client';

import { Fragment, ReactNode, useCallback, useEffect, useRef, useState } from 'react';
import { usePathname } from 'next/navigation';

import { usePlatformEvents } from '@/hooks/usePlatformEvents';
import {
  shouldRefreshStaffRoute,
  type StaffDataChangedPayload,
} from '@/lib/staff-realtime-scopes';

const REFRESH_DEBOUNCE_MS = 250;

export function StaffRealtimeBoundary({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const [revision, setRevision] = useState(0);
  const refreshTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const scheduleRefresh = useCallback((payload: StaffDataChangedPayload) => {
    if (!shouldRefreshStaffRoute(pathname, payload.scopes)) {
      return;
    }

    if (refreshTimer.current) {
      clearTimeout(refreshTimer.current);
    }

    refreshTimer.current = setTimeout(() => {
      refreshTimer.current = null;
      setRevision((current) => current + 1);
    }, REFRESH_DEBOUNCE_MS);
  }, [pathname]);

  usePlatformEvents({ onStaffDataChanged: scheduleRefresh });

  useEffect(() => () => {
    if (refreshTimer.current) {
      clearTimeout(refreshTimer.current);
    }
  }, [pathname]);

  return <Fragment key={`${pathname}:${revision}`}>{children}</Fragment>;
}

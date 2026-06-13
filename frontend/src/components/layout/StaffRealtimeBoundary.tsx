'use client';

import {
  createContext,
  ReactNode,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from 'react';
import { usePathname } from 'next/navigation';

import { usePlatformEvents } from '@/hooks/usePlatformEvents';
import {
  shouldRefreshStaffRoute,
  type StaffDataChangedPayload,
} from '@/lib/staff-realtime-scopes';

const REFRESH_DEBOUNCE_MS = 250;

/**
 * Context that exposes a monotonically-increasing revision counter.
 * Children subscribe to this via `useStaffRefresh()` and re-fetch data
 * when the value changes — **without** being remounted.
 */
const StaffRefreshContext = createContext<number>(0);

export function useStaffRefresh(): number {
  return useContext(StaffRefreshContext);
}

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

  return (
    <StaffRefreshContext.Provider value={revision}>
      {children}
    </StaffRefreshContext.Provider>
  );
}

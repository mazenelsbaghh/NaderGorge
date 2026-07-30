'use client';

import { usePlatformEvents } from '@/hooks/usePlatformEvents';

/** Mounts the staff realtime transport and its reconnect reconciliation once per staff shell. */
export function useStaffRealtimeInvalidation(): void {
  usePlatformEvents();
}

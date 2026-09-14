'use client';

import {
  ReactNode,
} from 'react';

import { useStaffRealtimeInvalidation } from '@/hooks/useStaffRealtimeInvalidation';
import { useStaffLiveSupportNotifications } from '@/hooks/useStaffLiveSupportNotifications';

export function StaffRealtimeBoundary({ children }: { children: ReactNode }) {
  useStaffRealtimeInvalidation();
  useStaffLiveSupportNotifications();

  return <>{children}</>;
}

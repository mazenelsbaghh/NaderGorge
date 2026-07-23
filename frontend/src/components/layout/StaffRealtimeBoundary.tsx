'use client';

import {
  ReactNode,
} from 'react';

import { useStaffRealtimeInvalidation } from '@/hooks/useStaffRealtimeInvalidation';

export function StaffRealtimeBoundary({ children }: { children: ReactNode }) {
  useStaffRealtimeInvalidation();

  return <>{children}</>;
}

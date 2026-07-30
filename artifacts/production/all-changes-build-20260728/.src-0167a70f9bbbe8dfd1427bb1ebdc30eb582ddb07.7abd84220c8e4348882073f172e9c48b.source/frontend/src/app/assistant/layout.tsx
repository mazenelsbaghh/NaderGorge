'use client';

import { useEffect } from 'react';
import { AssistantGuard } from '@/components/layout/AssistantGuard';
import { StaffRealtimeBoundary } from '@/components/layout/StaffRealtimeBoundary';

export default function AssistantLayout({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    document.documentElement.classList.add("admin-route-active");

    return () => {
      document.documentElement.classList.remove("admin-route-active");
    };
  }, []);

  return (
    <AssistantGuard>
      <StaffRealtimeBoundary>{children}</StaffRealtimeBoundary>
    </AssistantGuard>
  );
}

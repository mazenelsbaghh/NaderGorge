'use client';

import React from 'react';
import { AssistantGuard } from '@/components/layout/AssistantGuard';
import { StaffRealtimeBoundary } from '@/components/layout/StaffRealtimeBoundary';

export default function AssistantLayout({ children }: { children: React.ReactNode }) {
  return (
    <AssistantGuard>
      <StaffRealtimeBoundary>{children}</StaffRealtimeBoundary>
    </AssistantGuard>
  );
}

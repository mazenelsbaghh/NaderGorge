'use client';

import React from 'react';
import { AssistantGuard } from '@/components/layout/AssistantGuard';

export default function AssistantLayout({ children }: { children: React.ReactNode }) {
  return (
    <AssistantGuard>
      {children}
    </AssistantGuard>
  );
}

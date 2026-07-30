'use client';

import { useEffect } from 'react';
import { usePathname } from 'next/navigation';
import { AssistantGuard } from '@/components/layout/AssistantGuard';
import { StaffRealtimeBoundary } from '@/components/layout/StaffRealtimeBoundary';
import {
  AssistantShellChrome,
  getAssistantShellDefaults,
} from '@/components/assistant/AssistantShellChrome';

export default function AssistantLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const shell = getAssistantShellDefaults(pathname);

  useEffect(() => {
    document.documentElement.classList.add("admin-route-active");

    return () => {
      document.documentElement.classList.remove("admin-route-active");
    };
  }, []);

  return (
    <AssistantGuard>
      <AssistantShellChrome {...shell} persistentRoot>
        <StaffRealtimeBoundary>{children}</StaffRealtimeBoundary>
      </AssistantShellChrome>
    </AssistantGuard>
  );
}

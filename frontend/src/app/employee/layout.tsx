'use client';

import { StaffGuard } from '@/components/layout/StaffGuard';
import { StaffRealtimeBoundary } from '@/components/layout/StaffRealtimeBoundary';

export default function EmployeeLayout({ children }: { children: React.ReactNode }) {
  return (
    <StaffGuard>
      <StaffRealtimeBoundary>
        <div
          dir="rtl"
          className="hr-theme min-h-dvh bg-[var(--admin-bg)] text-[var(--admin-text)]"
        >
          {children}
        </div>
      </StaffRealtimeBoundary>
    </StaffGuard>
  );
}

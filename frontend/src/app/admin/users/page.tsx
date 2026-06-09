'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';

export default function AdminUsersPage() {
  const router = useRouter();

  useEffect(() => {
    router.replace('/admin/students');
  }, [router]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-[var(--admin-bg)]">
      <div className="h-8 w-8 animate-spin rounded-full border-4 border-[var(--admin-primary)] border-t-transparent"></div>
    </div>
  );
}

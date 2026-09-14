'use client';

import Link from 'next/link';
import { Bell } from 'lucide-react';
import { PlatformLogo } from '@/components/shared/PlatformLogo';
import { UserAvatar } from '@/components/ui/UserAvatar';

type StudentMobileHeaderProps = {
  fullName?: string;
  avatarSlug?: string | null;
  unreadCount: number;
  isDark: boolean;
};

export function StudentMobileHeader({ fullName, avatarSlug, unreadCount, isDark }: StudentMobileHeaderProps) {
  return (
    <div className="flex items-center justify-between gap-4 lg:hidden" data-testid="student-mobile-header">
      <Link href="/student" aria-label="مسار، الرئيسية" className="relative flex h-20 w-36 items-center justify-center overflow-hidden rounded-lg focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]">
        <PlatformLogo variant="full" tone={isDark ? 'light' : 'dark'} priority className={isDark ? '!h-14 !w-14' : '!h-36 !w-36 max-w-none shrink-0'} />
      </Link>
      <div className="flex items-center gap-3">
        <Link href="/student/profile" aria-label="الملف الشخصي" className="flex h-11 w-11 items-center justify-center rounded-full bg-[var(--admin-card-strong)] text-xl font-bold text-[var(--admin-text)] focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]">
          {avatarSlug ? <UserAvatar fullName={fullName} avatarSlug={avatarSlug} size="md" /> : (fullName?.trim().charAt(0) || 'ط')}
        </Link>
        <Link href="/student/notifications" aria-label={unreadCount > 0 ? `الإشعارات، ${unreadCount} غير مقروءة` : 'الإشعارات'} className="relative flex h-11 w-11 items-center justify-center rounded-full bg-[var(--admin-card-soft)] text-[var(--admin-text)] focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]">
          <Bell className="h-6 w-6" aria-hidden="true" />
          {unreadCount > 0 && <span className="absolute end-1.5 top-1 h-2.5 w-2.5 rounded-full bg-[#D4A017]" aria-hidden="true" />}
        </Link>
      </div>
    </div>
  );
}

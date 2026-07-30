'use client';

import Link from 'next/link';
import { Home, Menu, type LucideIcon } from 'lucide-react';

export type StudentBottomNavItem = {
  href: string;
  label: string;
  icon: LucideIcon;
};

type StudentBottomNavProps = {
  activePath: string;
  primaryItems: StudentBottomNavItem[];
  drawerHasCurrentPage: boolean;
  drawerId: string;
  isDrawerOpen: boolean;
  onOpenDrawer: () => void;
  unreadCount: number;
};

export function StudentBottomNav({
  activePath,
  primaryItems,
  drawerHasCurrentPage,
  drawerId,
  isDrawerOpen,
  onOpenDrawer,
  unreadCount,
}: StudentBottomNavProps) {
  const visiblePrimaryItems = primaryItems.slice(0, 3);

  return (
    <nav
      className="fixed inset-x-0 bottom-0 z-40 bg-[var(--admin-sidebar)]/95 px-3 pb-[max(0.5rem,env(safe-area-inset-bottom))] pt-1.5 backdrop-blur-xl lg:hidden"
      aria-label="القائمة السفلية للطالب"
    >
      <div className="mx-auto flex w-full max-w-md items-center justify-around gap-1">
        <Link
          href="/student"
          aria-current={activePath === '/student' ? 'page' : undefined}
          className={`flex min-h-11 flex-col items-center justify-center gap-0.5 rounded-2xl px-3 py-1.5 text-center transition-all focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${
            activePath === '/student' ? 'text-[var(--admin-primary)]' : 'text-[var(--admin-muted)]'
          }`}
        >
          <Home className="h-[22px] w-[22px]" aria-hidden="true" />
          <span className="text-xs font-bold leading-none">الرئيسية</span>
        </Link>

        {visiblePrimaryItems.map((item) => {
          const Icon = item.icon;
          const isActive = item.href === activePath;

          return (
            <Link
              key={item.href}
              href={item.href}
              aria-current={isActive ? 'page' : undefined}
              className={`flex min-h-11 flex-col items-center justify-center gap-0.5 rounded-2xl px-3 py-1.5 text-center transition-all focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${
                isActive ? 'text-[var(--admin-primary)]' : 'text-[var(--admin-muted)]'
              }`}
            >
              <Icon className="h-[22px] w-[22px]" aria-hidden="true" />
              <span className="text-xs font-bold leading-none">{item.label}</span>
            </Link>
          );
        })}

        <button
          type="button"
          onClick={onOpenDrawer}
          className={`relative flex min-h-11 flex-col items-center justify-center gap-0.5 rounded-2xl px-3 py-1.5 text-center transition-all focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${
            isDrawerOpen || drawerHasCurrentPage
              ? 'text-[var(--admin-primary)]'
              : 'text-[var(--admin-muted)]'
          }`}
          aria-label="القائمة"
          aria-current={drawerHasCurrentPage ? 'page' : undefined}
          aria-expanded={isDrawerOpen}
          aria-controls={drawerId}
        >
          <span className="relative">
            <Menu className="h-[22px] w-[22px]" aria-hidden="true" />
            {unreadCount > 0 && (
              <span
                className="absolute -left-0.5 -top-0.5 h-2 w-2 rounded-full bg-[var(--admin-primary)]"
                aria-hidden="true"
              />
            )}
          </span>
          <span className="text-xs font-bold leading-none">القائمة</span>
        </button>
      </div>
    </nav>
  );
}

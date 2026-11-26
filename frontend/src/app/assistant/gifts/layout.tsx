'use client';

import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function AssistantGiftsLayout({ children }: { children: React.ReactNode }) {
  return <NavRouteGuard routePath="/assistant/gifts" permission="gifts.manage">{children}</NavRouteGuard>;
}

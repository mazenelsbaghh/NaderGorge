import type { ReactNode } from 'react';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function StaffPublicExamsLayout({ children }: { children: ReactNode }) {
  return <NavRouteGuard routePath="/assistant/public-exams" permission="public_exams.manage">{children}</NavRouteGuard>;
}

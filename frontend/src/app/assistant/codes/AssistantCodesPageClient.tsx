'use client';

import AdminCodesPageClient from '@/app/admin/codes/AdminCodesPageClient';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function AssistantCodesPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/codes" permission="codes.manage">
      <AdminCodesPageClient mode="assistant" />
    </NavRouteGuard>
  );
}

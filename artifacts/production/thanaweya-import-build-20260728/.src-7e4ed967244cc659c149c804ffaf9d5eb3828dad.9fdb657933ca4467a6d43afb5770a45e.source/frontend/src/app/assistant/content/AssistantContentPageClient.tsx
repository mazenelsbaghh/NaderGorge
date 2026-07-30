'use client';

import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import AdminContentPageClient from '@/app/admin/content/AdminContentPageClient';

export default function AssistantContentPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/content" permission="content.manage">
      <AdminContentPageClient mode="assistant" />
    </NavRouteGuard>
  );
}

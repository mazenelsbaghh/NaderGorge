'use client';

import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import AdminCommunityPageClient from '@/app/admin/community/AdminCommunityPageClient';

export default function AssistantCommunityPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/community" permission="community.manage">
      <AdminCommunityPageClient mode="assistant" />
    </NavRouteGuard>
  );
}

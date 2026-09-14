'use client';

import CodeGroupDetailsPageClient from '@/app/admin/codes/[groupId]/CodeGroupDetailsPageClient';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function AssistantCodeGroupPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/codes" permission="codes.manage">
      <CodeGroupDetailsPageClient mode="assistant" />
    </NavRouteGuard>
  );
}

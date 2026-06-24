'use client';

import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import WatchRequestsPageClient from '@/app/admin/watch-requests/WatchRequestsPageClient';

export default function AssistantWatchRequestsPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/watch-requests" permission="watch_requests.manage">
      <WatchRequestsPageClient mode="assistant" />
    </NavRouteGuard>
  );
}

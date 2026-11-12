'use client';

import Link from 'next/link';
import { Bell } from 'lucide-react';
import { useCallback, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import { usePlatformQuery, usePlatformQueryClient } from '@/components/providers/QueryProvider';
import { usePlatformEvents } from '@/hooks/usePlatformEvents';
import { queryKeys } from '@/lib/query-keys';
import { adminService } from '@/services/admin-service';

type WatchRequestsHref = '/admin/watch-requests' | '/assistant/watch-requests';
const formatCount = new Intl.NumberFormat('ar-EG');
const REFRESH_INTERVAL_MS = 60_000;

export function useWatchRequestAlerts(href: WatchRequestsHref | null, userId: string | undefined) {
  const enabled = Boolean(href && userId);
  const client = usePlatformQueryClient();
  const { data } = usePlatformQuery({
    queryKey: queryKeys.admin.pendingWatchRequestCount(userId ?? ''),
    queryFn: adminService.getPendingWatchRequestCount,
    staleTime: REFRESH_INTERVAL_MS,
    enabled,
  });
  const toastId = `watch-request-alert:${href}:${userId}`;
  const lastAlert = useRef<{ toastId: string; requestId: string } | null>(null);

  const refresh = useCallback(() => {
    if (href && userId) client.invalidateQueries(queryKeys.admin.pendingWatchRequestCount(userId));
  }, [client, href, userId]);

  const { isConnected } = usePlatformEvents({
    onExtraWatchRequestCreated: ({ requestId }) => {
      refresh();
      if (!href || !userId || !requestId ||
          (lastAlert.current?.toastId === toastId && lastAlert.current.requestId === requestId)) return;

      lastAlert.current = { toastId, requestId };
      toast((item) => (
        <Link href={href} onClick={() => toast.dismiss(item.id)} dir="rtl"
          className="flex min-h-11 flex-wrap items-center gap-x-3 gap-y-1 rounded-sm font-bold focus-visible:outline-2 focus-visible:outline-offset-2">
          <Bell className="size-5 shrink-0" aria-hidden="true" />
          <span>طلب مشاهدة إضافية جديد</span>
          <span className="text-sm underline underline-offset-4">راجع الطلبات</span>
        </Link>
      ), { id: toastId, duration: 10_000 });
    },
    onExtraWatchRequestUpdated: refresh,
    onStaffDataChanged: (event) => {
      if (event.scopes.includes('watch-requests')) refresh();
    },
  });

  useEffect(() => {
    if (!enabled) return;

    const refreshWhileVisible = () => {
      if (document.visibilityState === 'visible') refresh();
    };
    const interval = window.setInterval(refreshWhileVisible, REFRESH_INTERVAL_MS);
    document.addEventListener('visibilitychange', refreshWhileVisible);
    return () => {
      window.clearInterval(interval);
      document.removeEventListener('visibilitychange', refreshWhileVisible);
      toast.dismiss(toastId);
    };
  }, [enabled, refresh, toastId]);

  useEffect(() => {
    if (isConnected) refresh();
  }, [isConnected, refresh]);

  return enabled ? data : undefined;
}

export function WatchRequestAlertLink({ href, count, compact = false }: {
  href: WatchRequestsHref;
  count?: number;
  compact?: boolean;
}) {
  const hasPending = count !== undefined && count > 0;
  const label = count === undefined
    ? 'طلبات المشاهدة الإضافية'
    : `طلبات المشاهدة الإضافية، عدد الطلبات المعلّقة ${formatCount.format(count)}`;
  const badge = hasPending && (
    <span className={`inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-[var(--admin-primary)] px-1 text-[11px] font-black text-[var(--admin-primary-contrast)] ${compact ? 'absolute -end-1 -top-1' : 'ms-auto'}`} aria-hidden="true" dir="ltr">
      {count > 99 ? `${formatCount.format(99)}+` : formatCount.format(count)}
    </span>
  );

  return (
    <Link href={href} aria-label={label} title={label}
      className={`relative flex min-h-11 items-center gap-3 rounded-xl text-[var(--admin-text)] hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${compact ? 'size-11 shrink-0 justify-center' : 'w-full px-3'}`}>
      <Bell className="size-5 shrink-0" aria-hidden="true" />
      {!compact && <span className="truncate text-sm font-bold">طلبات المشاهدة</span>}
      {badge}
    </Link>
  );
}

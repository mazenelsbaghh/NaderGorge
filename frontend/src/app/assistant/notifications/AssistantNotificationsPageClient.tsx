'use client';

import React, { useCallback, useEffect, useState } from 'react';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { Bell, Clock, RefreshCw } from 'lucide-react';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import { notificationsService, type PlatformNotificationDto } from '@/services/notifications-service';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';
import { formatCairoDateTime } from '@/lib/cairo-time';

export default function AssistantNotificationsPageClient() {
  const [notifications, setNotifications] = useState<PlatformNotificationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const load = useCallback(async () => {
    setLoading(true);
    try { setNotifications(await notificationsService.list()); }
    catch { toast.error('تعذر تحميل الإشعارات. حاول مرة أخرى.'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  const markRead = async (notification: PlatformNotificationDto) => {
    if (notification.readAt) return;
    try {
      await notificationsService.markRead(notification.id);
      setNotifications(items => items.map(item => item.id === notification.id ? { ...item, readAt: new Date().toISOString() } : item));
    } catch { toast.error('تعذر تحديث حالة الإشعار.'); }
  };

  return (
    <NavRouteGuard routePath="/assistant/notifications">
      <AssistantPage
      activePath="/assistant/notifications"
      sectionLabel="التنبيهات"
      pageTitle="مركز الإشعارات والتنبيهات"
      subtitle="تابع آخر التحديثات الخاصة بالمهام والإشعارات الإدارية وقرارات الموارد البشرية."
    >
      <div className="mx-auto max-w-4xl space-y-6 text-right" dir="rtl">
        <div className="flex justify-end"><NeumorphButton onClick={load} disabled={loading} intent="ghost" size="sm"><RefreshCw className={loading ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} /><span className="sr-only">تحديث</span></NeumorphButton></div>
        {loading ? <div className="h-24 animate-pulse rounded-3xl bg-[var(--admin-card-soft)]" /> : notifications.length === 0 ? <div className="rounded-3xl border border-dashed border-[var(--admin-border)] p-12 text-center text-sm text-[var(--admin-muted)]">لا توجد إشعارات حاليًا.</div> : notifications.map((notif) => {
          return (
            <button type="button" onClick={() => void markRead(notif)}
              key={notif.id}
              className={`flex w-full gap-4 items-start p-5 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] text-right hover:shadow-md transition duration-200 ${notif.readAt ? 'opacity-70' : ''}`}
            >
              <div className="p-3 rounded-2xl shrink-0 text-amber-500 bg-amber-50 dark:bg-amber-950/20">
                <Bell className="h-5 w-5" />
              </div>
              <div className="flex-1 space-y-1">
                <div className="flex justify-between items-center">
                  <h3 className="text-sm font-black text-[var(--admin-text)]">{notif.title}</h3>
                  <span className="text-xs text-[var(--admin-muted)] font-bold font-mono flex items-center gap-1">
                    <Clock className="h-3 w-3" />
                    {formatCairoDateTime(notif.createdAt)}
                  </span>
                </div>
                <p className="text-xs text-[var(--admin-muted)] leading-relaxed">{notif.body}</p>
              </div>
            </button>
          );
        })}
      </div>
    </AssistantPage>
    </NavRouteGuard>
  );
}

"use client";

import { useState, useEffect } from "react";
import { motion } from "framer-motion";
import { Bell, Check, Clock, RefreshCw, Sparkles } from "lucide-react";
import { AsyncRegionState } from "@/components/ui/AsyncRegionState";
import { studentService, StudentNotificationDto } from "@/services/student-service";
import { registerCacheStore } from "@/lib/cache-invalidation";

export default function StudentNotificationsPageClient() {
  const [notifications, setNotifications] = useState<StudentNotificationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actioningId, setActioningId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const fetchNotifications = () => {
    setLoading(true);
    setError(null);
    studentService.getNotifications()
      .then((res) => {
        setNotifications(res);
      })
      .catch((err) => {
        console.error("Error fetching notifications:", err);
        setError("تعذر تحميل الإشعارات. حاول مرة أخرى.");
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchNotifications();
    const cleanupCacheStore = registerCacheStore('notifications', () => {}, fetchNotifications);
    return cleanupCacheStore;
  }, []);

  const handleMarkAsRead = async (id: string) => {
    setActioningId(id);
    setActionError(null);
    try {
      await studentService.markNotificationAsRead(id);
      // Update locally
      setNotifications((prev) =>
        prev.map((notif) => (notif.id === id ? { ...notif, isRead: true } : notif))
      );
      // Dispatch a custom event to notify StudentShellChrome to update the badge count
      if (typeof window !== "undefined") {
        window.dispatchEvent(new Event("notificationsUpdated"));
      }
    } catch (err) {
      console.error("Error marking notification as read:", err);
      setActionError("تعذر تحديث الإشعار. حاول مرة أخرى.");
    } finally {
      setActioningId(null);
    }
  };

  if (loading) {
    return (
      <div className="mx-auto max-w-4xl" dir="rtl">
        <AsyncRegionState status="loading" message="جاري تحميل إشعاراتك" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex h-[60vh] items-center justify-center px-4" dir="rtl">
        <div className="max-w-md space-y-4 text-center">
          <p role="alert" className="text-base font-bold text-[var(--admin-danger)]">{error}</p>
          <p className="text-sm leading-7 text-[var(--admin-muted)]">تحقق من اتصالك، ثم حاول تحميل الصفحة مرة أخرى.</p>
          <button
            type="button"
            onClick={fetchNotifications}
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 py-3 text-sm font-bold text-[var(--admin-primary-contrast)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            إعادة المحاولة
          </button>
        </div>
      </div>
    );
  }

  return (
    <motion.div
      className="space-y-8 max-w-4xl mx-auto"
      initial={false}
      dir="rtl"
    >
      {/* Page Header */}
      <div className="rounded-2xl bg-[var(--admin-card-soft)] p-6 md:p-8">
        <div className="flex flex-col justify-between gap-6 md:flex-row md:items-center">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary-15)] px-4 py-1 text-xs font-black text-[var(--admin-primary)]">
              <Sparkles className="h-3.5 w-3.5" />
              صندوق الوارد والتنبيهات
            </div>
            <h1 className="mt-4 text-3xl font-black text-[var(--admin-text)] md:text-4xl">
              الإشعارات
            </h1>
            <p className="mt-2 text-sm text-[var(--admin-muted)]">
              تابع الإعلانات الهامة والتنبيهات المدرسية المخصصة لك من المعلمين وإدارة المنصة.
            </p>
          </div>
        </div>
      </div>

      {/* Notifications List */}
      <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6">
        {actionError && (
          <div role="alert" className="mb-4 flex items-center justify-between gap-3 rounded-xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-4 py-3 text-xs font-bold text-[var(--admin-danger)]">
            <span>{actionError}</span>
            <button type="button" onClick={() => setActionError(null)} className="underline">إخفاء</button>
          </div>
        )}
        <div className="flex items-center justify-between border-b border-[var(--admin-border)] pb-4 mb-6">
          <h2 className="text-lg font-black text-[var(--admin-text)] flex items-center gap-2">
            <Bell className="h-5 w-5 text-[var(--admin-primary)]" />
            التنبيهات الواردة
          </h2>
          <span className="text-xs bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-black px-3 py-1 rounded-full">
            {notifications.filter((n) => !n.isRead).length} غير مقروء
          </span>
        </div>

        <div className="space-y-4">
          {notifications.length === 0 ? (
            <div className="text-center py-16 space-y-3">
              <div className="bg-[var(--admin-bg)] p-4 rounded-full inline-flex text-[var(--admin-muted)]">
                <Bell className="h-10 w-10" />
              </div>
              <h4 className="font-bold text-[var(--admin-text)] text-sm">صندوق إشعاراتك فارغ</h4>
              <p className="mx-auto max-w-md text-sm leading-7 text-[var(--admin-muted)]">الإشعارات العامة أو المطابقة لبياناتك الدراسية ستظهر هنا عند وصولها. لا يلزمك إجراء الآن.</p>
            </div>
          ) : (
            notifications.map((notif) => (
              <div
                key={notif.id}
                className={`p-5 rounded-2xl border transition flex flex-col md:flex-row md:items-center justify-between gap-4 ${
                  notif.isRead
                    ? "bg-[var(--admin-card)] border-[var(--admin-border)]/60 opacity-75"
                    : "bg-[var(--admin-primary-15)]/30 border-[var(--admin-primary)]/20 shadow-sm"
                }`}
              >
                <div className="space-y-2">
                  <div className="flex items-center gap-2.5">
                    {!notif.isRead && (
                      <span className="h-2 w-2 rounded-full bg-[var(--admin-primary)] shrink-0" />
                    )}
                    <h3 className={`font-bold text-[var(--admin-text)] text-sm ${!notif.isRead ? "text-[var(--admin-primary)]" : ""}`}>
                      {notif.title}
                    </h3>
                  </div>
                  <p className="text-xs text-[var(--admin-text)]/90 leading-relaxed font-medium">
                    {notif.body}
                  </p>
                  <span className="text-xs text-[var(--admin-muted)] flex items-center gap-1 mt-1">
                    <Clock className="h-3.5 w-3.5" />
                    {new Date(notif.createdAt).toLocaleDateString("ar-EG-u-nu-latn", { timeZone: 'Africa/Cairo',
                      year: "numeric",
                      month: "long",
                      day: "numeric",
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </span>
                </div>

                {!notif.isRead && (
                  <button
                    onClick={() => handleMarkAsRead(notif.id)}
                    disabled={actioningId === notif.id}
                    className="flex min-h-11 items-center gap-2 self-stretch rounded-xl bg-[var(--admin-primary)] px-4 py-2 text-sm font-bold text-[var(--admin-primary-contrast)] transition-colors hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 disabled:cursor-wait disabled:opacity-50 md:self-center"
                  >
                    <Check className="h-3.5 w-3.5" />
                    تحديد كمقروء
                  </button>
                )}
              </div>
            ))
          )}
        </div>
      </div>
    </motion.div>
  );
}

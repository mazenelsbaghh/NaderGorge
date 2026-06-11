"use client";

import { useState, useEffect } from "react";
import { motion } from "framer-motion";
import { Bell, Check, Clock, Sparkles } from "lucide-react";
import { studentService, StudentNotificationDto } from "@/services/student-service";
import { useStudentTheme } from "@/hooks/useStudentTheme";
import { fadeSlideUp } from "@/lib/motion";

import { registerCacheStore, unregisterCacheStore } from "@/lib/cache-invalidation";

export default function StudentNotificationsPageClient() {
  const { isReady } = useStudentTheme();
  const [notifications, setNotifications] = useState<StudentNotificationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [actioningId, setActioningId] = useState<string | null>(null);

  const fetchNotifications = () => {
    studentService.getNotifications()
      .then((res) => {
        setNotifications(res);
      })
      .catch((err) => console.error("Error fetching notifications:", err))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchNotifications();
    registerCacheStore('notifications', () => {}, fetchNotifications);
    return () => {
      unregisterCacheStore('notifications');
    };
  }, []);

  const handleMarkAsRead = async (id: string) => {
    setActioningId(id);
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
    } finally {
      setActioningId(null);
    }
  };

  if (loading) {
    return (
      <div className="flex h-[60vh] items-center justify-center" dir="rtl">
        <div className="text-center space-y-4">
          <div className="h-12 w-12 border-4 border-[var(--admin-primary)] border-t-transparent rounded-full animate-spin mx-auto"></div>
          <p className="text-sm text-[var(--admin-muted)]">جاري تحميل الإشعارات...</p>
        </div>
      </div>
    );
  }

  return (
    <motion.div
      className="space-y-8 max-w-4xl mx-auto"
      variants={fadeSlideUp}
      initial="hidden"
      animate={isReady ? "show" : undefined}
      dir="rtl"
    >
      {/* Page Header */}
      <div className="relative overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,var(--admin-primary-15),transparent_42%)]" />
        <div className="relative flex flex-col md:flex-row md:items-center justify-between gap-6">
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
      <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-xl">
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
              <p className="text-xs text-[var(--admin-muted)]">عندما يرسل المعلمون إعلانات جديدة، ستظهر هنا.</p>
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
                  <span className="text-[10px] text-[var(--admin-muted)] flex items-center gap-1 mt-1">
                    <Clock className="h-3.5 w-3.5" />
                    {new Date(notif.createdAt).toLocaleDateString("ar-EG", {
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
                    className="self-start md:self-center bg-[var(--admin-primary)] hover:brightness-110 text-[var(--admin-primary-contrast)] font-bold text-xs px-4 py-2 rounded-xl flex items-center gap-1.5 transition active:scale-95 disabled:opacity-50"
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

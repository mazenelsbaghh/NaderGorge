'use client';

import { useEffect, useState } from 'react';
import { Play, Square, Clock, Loader2, Award, Zap } from 'lucide-react';
import { hrService, AttendanceLogDto } from '@/services/hr-service';
import toast from 'react-hot-toast';

export function ClockInOutWidget() {
  const [activeSession, setActiveSession] = useState<AttendanceLogDto | null>(
    null
  );
  const [loading, setLoading] = useState<boolean>(true);
  const [actionLoading, setActionLoading] = useState<boolean>(false);

  // Live Clock & Stopwatch states
  const [now, setNow] = useState<Date | null>(null);
  const [stopwatch, setStopwatch] = useState<string>('00:00:00');

  // Load active session
  const checkActiveSession = async () => {
    try {
      const logs = await hrService.getMyAttendance();
      const active = logs.find((log) => !log.clockOut);
      setActiveSession(active || null);
    } catch {
      // API client will toast error if status is not 401
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    checkActiveSession();
  }, []);

  // Update live clock
  useEffect(() => {
    setNow(new Date());
    const interval = setInterval(() => {
      setNow(new Date());
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  // Update stopwatch if there is an active session
  useEffect(() => {
    if (!activeSession) {
      setStopwatch('00:00:00');
      return;
    }

    const updateStopwatch = () => {
      const clockInTime = new Date(activeSession.clockIn).getTime();
      const diffMs = Date.now() - clockInTime;
      if (diffMs < 0) {
        setStopwatch('00:00:00');
        return;
      }
      const diffSecs = Math.floor(diffMs / 1000);
      const hours = Math.floor(diffSecs / 3600);
      const minutes = Math.floor((diffSecs % 3600) / 60);
      const seconds = diffSecs % 60;

      const pad = (n: number) => n.toString().padStart(2, '0');
      setStopwatch(`${pad(hours)}:${pad(minutes)}:${pad(seconds)}`);
    };

    updateStopwatch();
    const interval = setInterval(updateStopwatch, 1000);
    return () => clearInterval(interval);
  }, [activeSession]);

  const handleClockIn = async () => {
    setActionLoading(true);
    try {
      const res = await hrService.clockIn();
      if (res.success) {
        toast.success('تم تسجيل الحضور بنجاح 🟢');
        await checkActiveSession();
      } else {
        toast.error(res.message || 'فشل تسجيل الحضور');
      }
    } catch (err: any) {
      toast.error(
        err?.response?.data?.message || 'حدث خطأ أثناء محاولة تسجيل الحضور.'
      );
    } finally {
      setActionLoading(false);
    }
  };

  const handleClockOut = async () => {
    setActionLoading(true);
    try {
      const res = await hrService.clockOut();
      if (res.success) {
        toast.success('تم تسجيل الانصراف بنجاح 🔴');
        setActiveSession(null);
        await checkActiveSession();
      } else {
        toast.error(res.message || 'فشل تسجيل الانصراف');
      }
    } catch (err: any) {
      toast.error(
        err?.response?.data?.message || 'حدث خطأ أثناء محاولة تسجيل الانصراف.'
      );
    } finally {
      setActionLoading(false);
    }
  };

  const formatTime = (date: Date) => {
    return date.toLocaleTimeString('ar-EG', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  };

  const formatDate = (date: Date) => {
    return date.toLocaleDateString('ar-EG', {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  };

  if (loading) {
    return (
      <div className="flex h-48 items-center justify-center rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-muted)]">
        <Loader2 className="h-6 w-6 animate-spin text-[var(--admin-primary)]" />
        <span className="mr-2 text-sm font-bold">
          جاري تحميل بيانات الحضور...
        </span>
      </div>
    );
  }

  return (
    <div className="relative overflow-hidden rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-md transition-all duration-300 hover:shadow-lg">
      {/* Background patterns */}
      <div className="absolute -left-16 -top-16 h-32 w-32 rounded-full bg-[var(--admin-primary)]/5 blur-2xl" />
      <div className="absolute -bottom-16 -right-16 h-32 w-32 rounded-full bg-[var(--admin-primary)]/5 blur-2xl" />

      <div className="relative flex flex-col md:flex-row items-center justify-between gap-6">
        {/* Time and Info Section */}
        <div className="flex flex-col text-center md:text-right w-full md:w-auto">
          <div className="flex items-center justify-center md:justify-start gap-2 text-[var(--admin-primary)] text-sm font-extrabold mb-1">
            <Clock className="h-4 w-4" />
            <span>لوحة الحضور والانصراف</span>
          </div>

          <h3 className="text-3xl font-black text-[var(--admin-text)] tracking-tight font-mono">
            {now ? formatTime(now) : '00:00:00'}
          </h3>
          <p className="text-xs text-[var(--admin-muted)] mt-1 font-bold">
            {now ? formatDate(now) : ''}
          </p>

          {activeSession && (
            <div className="mt-4 flex items-center justify-center md:justify-start gap-2 rounded-xl bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs text-[var(--admin-primary)] font-bold">
              <Zap className="h-3.5 w-3.5 animate-pulse" />
              <span>
                بداية الوردية:{' '}
                {new Date(activeSession.clockIn).toLocaleTimeString('ar-EG', {
                  hour: '2-digit',
                  minute: '2-digit',
                })}
              </span>
              {activeSession.lateMinutes > 0 && (
                <span className="mr-2 rounded-full bg-red-100 dark:bg-red-950/40 text-red-600 px-2 py-0.5 text-[10px]">
                  متأخر {activeSession.lateMinutes} دقيقة
                </span>
              )}
            </div>
          )}
        </div>

        {/* Stopwatch and Button Action Section */}
        <div className="flex flex-col items-center gap-3 w-full md:w-auto">
          {activeSession ? (
            <>
              {/* Stopwatch display */}
              <div className="text-center">
                <p className="text-[10px] uppercase tracking-widest font-black text-[var(--admin-muted)]">
                  مدة العمل المستمرة
                </p>
                <div className="text-2xl font-black text-[var(--admin-text)] tracking-wider font-mono mt-0.5">
                  {stopwatch}
                </div>
              </div>

              <button
                type="button"
                onClick={handleClockOut}
                disabled={actionLoading}
                className="flex w-full md:w-44 items-center justify-center gap-2 rounded-2xl bg-red-500 py-3.5 text-sm font-extrabold text-white transition-all hover:bg-red-600 hover:shadow-lg active:scale-95 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {actionLoading ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Square className="h-4 w-4 fill-current" />
                )}
                تسجيل الانصراف
              </button>
            </>
          ) : (
            <>
              <div className="text-center">
                <p className="text-[10px] uppercase tracking-widest font-black text-[var(--admin-muted)]">
                  حالة الوردية
                </p>
                <div className="text-sm font-extrabold text-amber-500 mt-1 flex items-center justify-center gap-1">
                  <Award className="h-4 w-4" />
                  لم يتم تسجيل الحضور اليوم
                </div>
              </div>

              <button
                type="button"
                onClick={handleClockIn}
                disabled={actionLoading}
                className="flex w-full md:w-44 items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)] transition-all hover:bg-[var(--admin-primary-strong)] hover:shadow-lg active:scale-95 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {actionLoading ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Play className="h-4 w-4 fill-current" />
                )}
                تسجيل الحضور
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

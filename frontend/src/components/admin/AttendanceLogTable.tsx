'use client';

import { AttendanceLogDto } from '@/services/hr-service';
import {
  Calendar,
  Monitor,
  MapPin,
  CheckCircle,
  AlertTriangle,
  Moon,
  ShieldAlert,
} from 'lucide-react';

interface AttendanceLogTableProps {
  logs: AttendanceLogDto[];
  loading?: boolean;
}

export function AttendanceLogTable({ logs, loading }: AttendanceLogTableProps) {
  const getStatusBadge = (status: string | number) => {
    const s = typeof status === 'number' ? {
      0: 'Present',
      1: 'Late',
      2: 'Absent',
      3: 'Sick',
      4: 'Leave'
    }[status] || 'Present' : status;

    switch (s) {
      case 'Present':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 dark:bg-emerald-950/40 px-2.5 py-1 text-xs font-bold text-emerald-700 dark:text-emerald-400">
            <CheckCircle className="h-3.5 w-3.5" />
            حاضر
          </span>
        );
      case 'Late':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 dark:bg-amber-950/40 px-2.5 py-1 text-xs font-bold text-amber-700 dark:text-amber-400">
            <AlertTriangle className="h-3.5 w-3.5" />
            متأخر
          </span>
        );
      case 'Absent':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-rose-100 dark:bg-rose-950/40 px-2.5 py-1 text-xs font-bold text-rose-700 dark:text-rose-400">
            <ShieldAlert className="h-3.5 w-3.5" />
            غائب
          </span>
        );
      case 'Sick':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-sky-100 dark:bg-sky-950/40 px-2.5 py-1 text-xs font-bold text-sky-700 dark:text-sky-400">
            <Moon className="h-3.5 w-3.5" />
            مرضي
          </span>
        );
      case 'Leave':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-purple-100 dark:bg-purple-950/40 px-2.5 py-1 text-xs font-bold text-purple-700 dark:text-purple-400">
            <Calendar className="h-3.5 w-3.5" />
            إجازة
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-[var(--admin-card-strong)] px-2.5 py-1 text-xs font-bold text-[var(--admin-text)]">
            {status}
          </span>
        );
    }
  };

  const formatDateTime = (isoString: string) => {
    return new Date(isoString).toLocaleTimeString('ar-EG', {
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const formatDuration = (mins?: number) => {
    if (mins === undefined || mins === null) return '—';
    const hrs = Math.floor(mins / 60);
    const remainingMins = Math.round(mins % 60);
    if (hrs > 0) {
      return `${hrs} س و ${remainingMins} د`;
    }
    return `${remainingMins} دقيقة`;
  };

  if (loading) {
    return (
      <div className="space-y-3">
        {[1, 2, 3].map((n) => (
          <div
            key={n}
            className="h-16 w-full animate-pulse rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)]"
          />
        ))}
      </div>
    );
  }

  if (logs.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center rounded-3xl border border-dashed border-[var(--admin-border)] p-12 text-center text-[var(--admin-muted)] bg-[var(--admin-card-soft)]">
        <Calendar className="h-12 w-12 text-[var(--admin-border)] mb-4" />
        <h5 className="font-bold text-[var(--admin-text)]">
          لا توجد سجلات حضور
        </h5>
        <p className="text-xs mt-1">
          لم يتم تسجيل أي حضور أو انصراف لهذا الموظف بعد.
        </p>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-sm">
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-right text-sm">
          <thead>
            <tr className="border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-xs font-bold text-[var(--admin-muted)]">
              <th className="px-5 py-4">التاريخ</th>
              <th className="px-5 py-4">وقت الحضور</th>
              <th className="px-5 py-4">وقت الانصراف</th>
              <th className="px-5 py-4">ساعات العمل</th>
              <th className="px-5 py-4">الحالة</th>
              <th className="px-5 py-4">التأخير</th>
              <th className="px-5 py-4 text-left">بيانات الوصول</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--admin-border)] text-[var(--admin-text)]">
            {logs.map((log) => (
              <tr
                key={log.id}
                className="group hover:bg-[var(--admin-hover)] transition-colors"
              >
                <td className="px-5 py-4 font-bold font-mono text-[var(--admin-text)]">
                  {log.date}
                </td>
                <td className="px-5 py-4 font-medium font-mono text-sm">
                  {formatDateTime(log.clockIn)}
                </td>
                <td className="px-5 py-4 font-medium font-mono text-sm">
                  {log.clockOut ? (
                    formatDateTime(log.clockOut)
                  ) : (
                    <span className="animate-pulse font-bold text-[var(--admin-primary)]">
                      نشط حالياً
                    </span>
                  )}
                </td>
                <td className="px-5 py-4 font-bold font-mono">
                  {formatDuration(log.durationMinutes)}
                </td>
                <td className="px-5 py-4">{getStatusBadge(log.status)}</td>
                <td className="px-5 py-4 font-bold text-red-500 font-mono">
                  {log.lateMinutes > 0 ? `${log.lateMinutes} د` : '—'}
                </td>
                <td className="px-5 py-4">
                  <div className="flex flex-col items-end gap-1 text-xs text-[var(--admin-muted)]">
                    <span className="flex items-center gap-1 font-mono">
                      {log.ipAddress || 'عنوان غير معروف'}
                      <MapPin className="h-3 w-3 text-[var(--admin-muted)]" />
                    </span>
                    <span
                      className="max-w-[180px] truncate flex items-center gap-1"
                      title={log.userAgent}
                    >
                      {log.userAgent || 'جهاز غير معروف'}
                      <Monitor className="h-3 w-3 text-[var(--admin-muted)]" />
                    </span>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

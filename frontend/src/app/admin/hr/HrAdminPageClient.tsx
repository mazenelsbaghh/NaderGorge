'use client';

import { useEffect, useState, useCallback } from 'react';
import {
  RefreshCw,
  Search,
  User,
  ExternalLink,
} from 'lucide-react';
import {
  AdminPage,
  AdminDataTable,
  AdminColumn,
} from '@/components/admin';
import {
  hrService,
  AdminAttendanceLogDto,
} from '@/services/hr-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import Link from 'next/link';
import { formatCairoDateTime } from '@/lib/cairo-time';

export default function HrAdminPageClient() {
  // Attendance states
  const [attendance, setAttendance] = useState<AdminAttendanceLogDto[]>([]);
  const [attendanceLoading, setAttendanceLoading] = useState<boolean>(true);
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [startDate, setStartDate] = useState<string>('');
  const [endDate, setEndDate] = useState<string>('');

  // Fetch attendance
  const fetchAttendance = useCallback(async () => {
    setAttendanceLoading(true);
    try {
      const data = await hrService.getAttendance(
        searchQuery || undefined,
        startDate || undefined,
        endDate || undefined
      );
      setAttendance(data);
    } catch {
      toast.error('تعذر تحميل سجلات الحضور');
    } finally {
      setAttendanceLoading(false);
    }
  }, [searchQuery, startDate, endDate]);

  useEffect(() => {
    fetchAttendance();
  }, [fetchAttendance]);

  // Badges helper
  const getStatusBadge = (status: string | number) => {
    const s = typeof status === 'number'
      ? ({ 0: 'Present', 1: 'Late', 2: 'Absent', 3: 'Sick', 4: 'Leave' }[status] || 'Present')
      : status;

    const maps: Record<string, { label: string; classes: string }> = {
      Present: {
        label: 'حاضر',
        classes:
          'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400',
      },
      Late: {
        label: 'متأخر',
        classes:
          'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400',
      },
      Absent: {
        label: 'غائب',
        classes:
          'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400',
      },
      Sick: {
        label: 'مرضي',
        classes: 'bg-[var(--admin-accent-soft)] text-[var(--admin-accent)]',
      },
      Leave: {
        label: 'إجازة',
        classes: 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]',
      },
    };

    const config = maps[s] || {
      label: String(s),
      classes: 'bg-[var(--admin-card-soft)] text-[var(--admin-text)]',
    };
    return (
      <span
        className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-bold ${config.classes}`}
      >
        <span className="h-1.5 w-1.5 rounded-full bg-current" />
        {config.label}
      </span>
    );
  };

  const formatTime = (isoString: string) => {
    return formatCairoDateTime(isoString, {
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const formatDuration = (mins?: number) => {
    if (mins === undefined || mins === null) return '—';
    const hrs = Math.floor(mins / 60);
    const rMins = Math.round(mins % 60);
    return hrs > 0 ? `${hrs} س و ${rMins} د` : `${rMins} د`;
  };

  const attendanceColumns: AdminColumn<AdminAttendanceLogDto>[] = [
    {
      key: 'employee',
      label: 'الموظف',
      render: (log) => (
        <div>
          <div className="font-bold text-[var(--admin-text)]">
            {log.employeeName}
          </div>
          <div className="text-xs text-[var(--admin-muted)] font-mono mt-0.5">
            {log.employeePhone}
          </div>
        </div>
      ),
    },
    {
      key: 'date',
      label: 'التاريخ',
      render: (log) => <span className="font-bold font-mono">{log.date}</span>,
    },
    {
      key: 'clockIn',
      label: 'حضور',
      render: (log) => (
        <span className="font-mono text-sm">{formatTime(log.clockIn)}</span>
      ),
    },
    {
      key: 'clockOut',
      label: 'انصراف',
      render: (log) => (
        <span className="font-mono text-sm">
          {log.clockOut ? (
            formatTime(log.clockOut)
          ) : (
            <span className="animate-pulse font-bold text-[var(--admin-primary)]">
              نشط حالياً
            </span>
          )}
        </span>
      ),
    },
    {
      key: 'duration',
      label: 'ساعات العمل',
      render: (log) => (
        <span className="font-mono">{formatDuration(log.durationMinutes)}</span>
      ),
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (log) => getStatusBadge(log.status),
    },
    {
      key: 'late',
      label: 'التأخير',
      render: (log) => (
        <span className="font-bold text-red-500 font-mono">
          {log.lateMinutes > 0 ? `${log.lateMinutes} د` : '—'}
        </span>
      ),
    },
  ];

  return (
    <AdminPage
      activePath="/admin/hr"
      sectionLabel="الموارد البشرية"
      pageTitle="إدارة شؤون الموظفين"
      subtitle="إدارة ومتابعة سجلات الحضور والانصراف وحساب التأخير والمدد. تتم إدارة الإجازات من مركز HR الجديد."
      action={
        <Link href="/admin/hr/my-attendance" prefetch={false}>
          <NeumorphButton intent="primary" size="lg" pill>
            <User className="h-4 w-4" />
            سجلاتي الشخصية
            <ExternalLink className="h-3 w-3 mr-1" />
          </NeumorphButton>
        </Link>
      }
    >
      {/* Main Panel Search & Filters */}
      <div className="hr-theme mb-6 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 flex flex-wrap gap-4 items-center justify-between">
        <>
            <div className="flex flex-1 min-w-[240px] items-center gap-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2">
              <Search className="h-4 w-4 text-[var(--admin-muted)]" />
              <input
                type="text"
                placeholder="ابحث بالاسم أو رقم الهاتف..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full bg-transparent text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none"
              />
            </div>
            <div className="flex flex-wrap items-center gap-3 w-full sm:w-auto">
              <div className="flex items-center gap-2">
                <span className="text-xs font-bold text-[var(--admin-muted)]">
                  من:
                </span>
                <input
                  type="date"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-xs text-[var(--admin-text)] outline-none"
                />
              </div>
              <div className="flex items-center gap-2">
                <span className="text-xs font-bold text-[var(--admin-muted)]">
                  إلى:
                </span>
                <input
                  type="date"
                  value={endDate}
                  onChange={(e) => setEndDate(e.target.value)}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-xs text-[var(--admin-text)] outline-none"
                />
              </div>
              <NeumorphButton
                intent="primary"
                size="md"
                onClick={fetchAttendance}
                disabled={attendanceLoading}
              >
                <RefreshCw
                  className={`h-4 w-4 ${attendanceLoading ? 'animate-spin' : ''}`}
                />
                تحديث
              </NeumorphButton>
            </div>
        </>
      </div>

      {/* Tables Display */}
      <AdminDataTable
        data={attendance}
        columns={attendanceColumns}
        loading={attendanceLoading}
        rowKey={(log) => log.id}
        emptyMessage="لا توجد سجلات حضور مطابقة للفلاتر المحددة."
      />
    </AdminPage>
  );
}

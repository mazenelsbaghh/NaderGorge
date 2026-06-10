'use client';

import { useEffect, useState, useCallback } from 'react';
import {
  RefreshCw,
  Search,
  Check,
  X as CloseIcon,
  Clock,
  Coffee,
  User,
  ExternalLink,
} from 'lucide-react';
import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
} from '@/components/admin';
import {
  hrService,
  AdminAttendanceLogDto,
  AdminVacationDto,
} from '@/services/hr-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import Link from 'next/link';

type ActiveTab = 'attendance' | 'vacations';

export default function HrAdminPageClient() {
  const [activeTab, setActiveTab] = useState<ActiveTab>('attendance');

  // Attendance states
  const [attendance, setAttendance] = useState<AdminAttendanceLogDto[]>([]);
  const [attendanceLoading, setAttendanceLoading] = useState<boolean>(true);
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [startDate, setStartDate] = useState<string>('');
  const [endDate, setEndDate] = useState<string>('');

  // Vacation states
  const [vacations, setVacations] = useState<AdminVacationDto[]>([]);
  const [vacationLoading, setVacationLoading] = useState<boolean>(true);
  const [vacationSearch, setVacationSearch] = useState<string>('');
  const [vacationStatusFilter, setVacationStatusFilter] = useState<string>('');

  // Submit state loading
  const [resolvingId, setResolvingId] = useState<string | null>(null);

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

  // Fetch vacations
  const fetchVacations = useCallback(async () => {
    setVacationLoading(true);
    try {
      const data = await hrService.getVacations(
        vacationSearch || undefined,
        vacationStatusFilter || undefined
      );
      setVacations(data);
    } catch {
      toast.error('تعذر تحميل طلبات الإجازات');
    } finally {
      setVacationLoading(false);
    }
  }, [vacationSearch, vacationStatusFilter]);

  useEffect(() => {
    if (activeTab === 'attendance') {
      fetchAttendance();
    } else {
      fetchVacations();
    }
  }, [activeTab, fetchAttendance, fetchVacations]);

  const handleApproveVacation = async (id: string) => {
    setResolvingId(id);
    try {
      const res = await hrService.approveVacation(id);
      if (res.success) {
        toast.success('تمت الموافقة على طلب الإجازة بنجاح ✅');
        fetchVacations();
      } else {
        toast.error(res.message || 'تعذر قبول الطلب');
      }
    } catch (err: any) {
      toast.error(
        err?.response?.data?.message || 'حدث خطأ أثناء معالجة الطلب.'
      );
    } finally {
      setResolvingId(null);
    }
  };

  const handleRejectVacation = async (id: string) => {
    setResolvingId(id);
    try {
      const res = await hrService.rejectVacation(id);
      if (res.success) {
        toast.success('تم رفض طلب الإجازة ❌');
        fetchVacations();
      } else {
        toast.error(res.message || 'تعذر رفض الطلب');
      }
    } catch (err: any) {
      toast.error(
        err?.response?.data?.message || 'حدث خطأ أثناء معالجة الطلب.'
      );
    } finally {
      setResolvingId(null);
    }
  };

  // Badges helper
  const getStatusBadge = (status: string | number, type: 'attendance' | 'vacation' = 'attendance') => {
    const s = typeof status === 'number'
      ? (type === 'attendance'
          ? ({ 0: 'Present', 1: 'Late', 2: 'Absent', 3: 'Sick', 4: 'Leave' }[status] || 'Present')
          : ({ 0: 'Pending', 1: 'Approved', 2: 'Rejected' }[status] || 'Pending'))
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
        classes: 'bg-sky-100 text-sky-700 dark:bg-sky-950/40 dark:text-sky-400',
      },
      Leave: {
        label: 'إجازة',
        classes:
          'bg-purple-100 text-purple-700 dark:bg-purple-950/40 dark:text-purple-400',
      },

      Pending: {
        label: 'قيد الانتظار',
        classes:
          'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400',
      },
      Approved: {
        label: 'مقبول',
        classes:
          'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400',
      },
      Rejected: {
        label: 'مرفوض',
        classes:
          'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400',
      },
    };

    const config = maps[s] || {
      label: String(s),
      classes: 'bg-gray-100 text-gray-700',
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
    return new Date(isoString).toLocaleTimeString('ar-EG', {
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

  const vacationColumns: AdminColumn<AdminVacationDto>[] = [
    {
      key: 'employee',
      label: 'الموظف',
      render: (v) => (
        <div>
          <div className="font-bold text-[var(--admin-text)]">
            {v.employeeName}
          </div>
          <div className="text-xs text-[var(--admin-muted)] font-mono mt-0.5">
            {v.employeePhone}
          </div>
        </div>
      ),
    },
    {
      key: 'range',
      label: 'الفترة الزمنية',
      render: (v) => (
        <div className="flex flex-col gap-0.5 text-xs font-bold font-mono">
          <span>من: {v.startDate}</span>
          <span>إلى: {v.endDate}</span>
        </div>
      ),
    },
    {
      key: 'reason',
      label: 'السبب',
      render: (v) => (
        <span className="max-w-[200px] truncate block text-sm" title={v.reason}>
          {v.reason}
        </span>
      ),
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (v) => getStatusBadge(v.status, 'vacation'),
    },
    {
      key: 'handler',
      label: 'معالجة بواسطة',
      render: (v) =>
        v.handledByName ? (
          <div>
            <div className="text-xs font-bold text-[var(--admin-text)]">
              {v.handledByName}
            </div>
            <div className="text-[10px] text-[var(--admin-muted)] font-mono">
              {v.handledAt
                ? new Date(v.handledAt).toLocaleDateString('ar-EG')
                : ''}
            </div>
          </div>
        ) : (
          <span className="text-xs text-[var(--admin-muted)]">—</span>
        ),
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (v) => {
        const isPending = v.status === 'Pending';
        const isLoading = resolvingId === v.id;

        if (!isPending) return null;

        return (
          <div className="flex items-center justify-end gap-2">
            <NeumorphButton
              type="button"
              onClick={() => handleApproveVacation(v.id)}
              disabled={isLoading || !!resolvingId}
              intent="primary"
              size="sm"
              title="قبول طلب الإجازة"
              className="!bg-emerald-500 !text-white hover:!bg-emerald-600 px-3 py-1.5 rounded-xl flex items-center gap-1 font-bold text-xs"
            >
              <Check className="h-3.5 w-3.5" />
              قبول
            </NeumorphButton>
            <NeumorphButton
              type="button"
              onClick={() => handleRejectVacation(v.id)}
              disabled={isLoading || !!resolvingId}
              intent="danger"
              size="sm"
              title="رفض طلب الإجازة"
              className="px-3 py-1.5 rounded-xl flex items-center gap-1 font-bold text-xs"
            >
              <CloseIcon className="h-3.5 w-3.5" />
              رفض
            </NeumorphButton>
          </div>
        );
      },
    },
  ];

  return (
    <AdminShellChrome
      activePath="/admin/hr"
      sectionLabel="الموارد البشرية"
      pageTitle="إدارة شؤون الموظفين"
      subtitle="إدارة ومتابعة سجلات الحضور والانصراف، وحساب التأخير والمدد، ومراجعة طلبات الإجازات الوظيفية."
      action={
        <Link href="/admin/hr/my-attendance">
          <NeumorphButton intent="primary" size="lg" pill>
            <User className="h-4 w-4" />
            سجلاتي الشخصية
            <ExternalLink className="h-3 w-3 mr-1" />
          </NeumorphButton>
        </Link>
      }
    >
      {/* Tabs Selector */}
      <div className="mb-8 flex justify-center">
        <div className="inline-flex gap-1 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-1.5 shadow-sm backdrop-blur-xl">
          <button
            onClick={() => setActiveTab('attendance')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'attendance'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <Clock className="h-4 w-4" />
            سجلات الحضور والانصراف
          </button>
          <button
            onClick={() => setActiveTab('vacations')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'vacations'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <Coffee className="h-4 w-4" />
            طلبات الإجازات
          </button>
        </div>
      </div>

      {/* Main Panel Search & Filters */}
      <div className="mb-6 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 flex flex-wrap gap-4 items-center justify-between">
        {activeTab === 'attendance' ? (
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
        ) : (
          <>
            <div className="flex flex-1 min-w-[240px] items-center gap-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2">
              <Search className="h-4 w-4 text-[var(--admin-muted)]" />
              <input
                type="text"
                placeholder="ابحث بالاسم أو رقم الهاتف..."
                value={vacationSearch}
                onChange={(e) => setVacationSearch(e.target.value)}
                className="w-full bg-transparent text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none"
              />
            </div>
            <div className="flex items-center gap-3 w-full sm:w-auto">
              <select
                value={vacationStatusFilter}
                onChange={(e) => setVacationStatusFilter(e.target.value)}
                className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2 text-xs text-[var(--admin-text)] outline-none"
              >
                <option value="">كل الحالات</option>
                <option value="Pending">قيد الانتظار</option>
                <option value="Approved">مقبول</option>
                <option value="Rejected">مرفوض</option>
              </select>
              <NeumorphButton
                intent="primary"
                size="md"
                onClick={fetchVacations}
                disabled={vacationLoading}
              >
                <RefreshCw
                  className={`h-4 w-4 ${vacationLoading ? 'animate-spin' : ''}`}
                />
                تحديث
              </NeumorphButton>
            </div>
          </>
        )}
      </div>

      {/* Tables Display */}
      {activeTab === 'attendance' ? (
        <AdminDataTable
          data={attendance}
          columns={attendanceColumns}
          loading={attendanceLoading}
          rowKey={(log) => log.id}
          emptyMessage="لا توجد سجلات حضور مطابقة للفلاتر المحددة."
        />
      ) : (
        <AdminDataTable
          data={vacations}
          columns={vacationColumns}
          loading={vacationLoading}
          rowKey={(v) => v.id}
          emptyMessage="لا توجد طلبات إجازة مطابقة للفلاتر المحددة."
        />
      )}
    </AdminShellChrome>
  );
}

'use client';

import { useEffect, useState, useCallback } from 'react';
import { RefreshCw, CalendarDays, Clock, Coffee } from 'lucide-react';
import {
  AdminShellChrome,
  AttendanceLogTable,
  VacationRequestModal,
} from '@/components/admin';
import { hrService, AttendanceLogDto } from '@/services/hr-service';
import NeumorphButton from '@/components/ui/neumorph-button';

export default function MyAttendancePage() {
  const [logs, setLogs] = useState<AttendanceLogDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<boolean>(false);
  const [showVacationModal, setShowVacationModal] = useState<boolean>(false);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const data = await hrService.getMyAttendance();
      setLogs(data);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  // Statistics
  const totalDays = logs.length;
  const lateDays = logs.filter((log) => log.status === 'Late').length;
  const totalHours = logs.reduce((acc, log) => {
    if (log.durationMinutes) {
      return acc + log.durationMinutes / 60;
    }
    return acc;
  }, 0);

  return (
    <AdminShellChrome
      activePath="/admin/hr/my-attendance"
      sectionLabel="سجلاتي"
      pageTitle="سجل الحضور والغياب"
      subtitle="عرض وتدقيق كافة عمليات تسجيل الحضور والانصراف والمدد الزمنية الخاصة بك."
      action={
        <div className="flex gap-2">
          <NeumorphButton
            intent="primary"
            size="lg"
            pill
            onClick={() => setShowVacationModal(true)}
          >
            <Coffee className="h-4 w-4" />
            طلب إجازة
          </NeumorphButton>
          <NeumorphButton
            intent="primary"
            size="lg"
            pill
            onClick={fetchLogs}
            disabled={loading}
          >
            <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
            تحديث السجل
          </NeumorphButton>
        </div>
      }
    >
      <VacationRequestModal
        open={showVacationModal}
        onClose={() => setShowVacationModal(false)}
        onSuccess={fetchLogs}
      />
      {/* Quick Summary Cards */}
      <section className="mb-10 grid grid-cols-1 gap-6 md:grid-cols-3">
        <div className="relative overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
          <div className="flex items-center gap-4">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-100 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-400">
              <CalendarDays className="h-6 w-6" />
            </div>
            <div>
              <p className="text-xs font-bold text-[var(--admin-muted)]">
                إجمالي الأيام المسجلة
              </p>
              <h4 className="text-2xl font-black text-[var(--admin-text)] mt-1 font-mono">
                {totalDays} يوم
              </h4>
            </div>
          </div>
        </div>

        <div className="relative overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
          <div className="flex items-center gap-4">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-amber-100 dark:bg-amber-950/40 text-amber-700 dark:text-amber-400">
              <Clock className="h-6 w-6" />
            </div>
            <div>
              <p className="text-xs font-bold text-[var(--admin-muted)]">
                أيام التأخير
              </p>
              <h4 className="text-2xl font-black text-amber-500 mt-1 font-mono">
                {lateDays} يوم
              </h4>
            </div>
          </div>
        </div>

        <div className="relative overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
          <div className="flex items-center gap-4">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
              <Clock className="h-6 w-6" />
            </div>
            <div>
              <p className="text-xs font-bold text-[var(--admin-muted)]">
                إجمالي ساعات العمل
              </p>
              <h4 className="text-2xl font-black text-[var(--admin-text)] mt-1 font-mono">
                {totalHours.toFixed(1)} ساعة
              </h4>
            </div>
          </div>
        </div>
      </section>

      {/* Main Content */}
      {error ? (
        <div className="flex flex-col items-center justify-center rounded-3xl border border-dashed border-[var(--admin-border)] p-12 text-center gap-4 bg-[var(--admin-card-soft)]">
          <span className="text-red-500 font-bold text-sm">
            حدث خطأ أثناء تحميل سجل الحضور الخاص بك.
          </span>
          <NeumorphButton onClick={fetchLogs} intent="primary" size="md">
            إعادة المحاولة
          </NeumorphButton>
        </div>
      ) : (
        <AttendanceLogTable logs={logs} loading={loading} />
      )}
    </AdminShellChrome>
  );
}

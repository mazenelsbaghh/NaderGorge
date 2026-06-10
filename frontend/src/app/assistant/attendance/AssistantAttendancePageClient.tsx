'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { ClockInOutWidget, AttendanceLogTable } from '@/components/admin';
import { hrService, AttendanceLogDto } from '@/services/hr-service';
import toast from 'react-hot-toast';

export default function AssistantAttendancePageClient() {
  const [logs, setLogs] = useState<AttendanceLogDto[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchAttendance = useCallback(async () => {
    setLoading(true);
    try {
      const res = await hrService.getMyAttendance();
      setLogs(res.logs ?? []);
    } catch {
      toast.error('حدث خطأ أثناء تحميل سجل الحضور الخاص بك');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAttendance();
  }, [fetchAttendance]);

  return (
    <AssistantShellChrome
      activePath="/assistant/attendance"
      sectionLabel="الموارد البشرية"
      pageTitle="سجل الحضور والانصراف"
      subtitle="سجل ورديات العمل اليومية الخاصة بك، ساعات العمل الإجمالية، وتوثيق أوقات الحضور والانصراف."
    >
      <div className="mx-auto max-w-5xl space-y-8 text-right animate-[fadeIn_0.4s_ease-out]" dir="rtl">
        {/* Clock In / Out Widget */}
        <ClockInOutWidget />

        {/* Attendance Log Table */}
        <div className="space-y-4">
          <h4 className="text-lg font-black text-[var(--admin-text)]">سجل ورديات الشهر الحالي</h4>
          <AttendanceLogTable logs={logs} loading={loading} />
        </div>
      </div>
    </AssistantShellChrome>
  );
}

'use client';

import { AdminPageSkeleton } from '@/components/admin';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

export default function AssistantAttendanceLoading() {
  return (
    <AssistantShellChrome
      activePath="/assistant/attendance"
      sectionLabel="الموارد البشرية"
      pageTitle="جاري تحميل سجل الحضور"
      subtitle="يتم تجهيز بيانات الحضور والانصراف الآن."
    >
      <AdminPageSkeleton />
    </AssistantShellChrome>
  );
}

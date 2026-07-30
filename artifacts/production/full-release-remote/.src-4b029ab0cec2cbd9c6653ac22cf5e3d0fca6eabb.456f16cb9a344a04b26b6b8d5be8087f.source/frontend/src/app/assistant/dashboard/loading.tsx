'use client';

import { AdminPageSkeleton } from '@/components/admin';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

export default function AssistantDashboardLoading() {
  return (
    <AssistantShellChrome
      activePath="/assistant/dashboard"
      sectionLabel="لوحة التحكم"
      pageTitle="جاري تحميل لوحة التحكم"
      subtitle="يتم تجهيز بيانات لوحة التحكم الآن."
    >
      <AdminPageSkeleton />
    </AssistantShellChrome>
  );
}

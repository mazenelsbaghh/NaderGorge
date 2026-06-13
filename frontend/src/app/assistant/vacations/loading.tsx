'use client';

import { AdminPageSkeleton } from '@/components/admin';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

export default function AssistantVacationsLoading() {
  return (
    <AssistantShellChrome
      activePath="/assistant/vacations"
      sectionLabel="الموارد البشرية"
      pageTitle="جاري تحميل طلبات الإجازة"
      subtitle="يتم تجهيز بيانات الإجازات الآن."
    >
      <AdminPageSkeleton />
    </AssistantShellChrome>
  );
}

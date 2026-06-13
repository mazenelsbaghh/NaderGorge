'use client';

import { AdminPageSkeleton } from '@/components/admin';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

export default function AssistantLoading() {
  return (
    <AssistantShellChrome
      activePath="/assistant/dashboard"
      sectionLabel="مساحة المساعدين"
      pageTitle="جاري تحميل الصفحة"
      subtitle="يتم تجهيز بيانات القسم الآن."
    >
      <AdminPageSkeleton />
    </AssistantShellChrome>
  );
}

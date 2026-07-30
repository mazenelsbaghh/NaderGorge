'use client';

import { AdminPageSkeleton } from '@/components/admin';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

export default function AssistantCrmLoading() {
  return (
    <AssistantShellChrome
      activePath="/assistant/crm"
      sectionLabel="متابعة الطلاب"
      pageTitle="جاري تحميل قائمة الاتصال"
      subtitle="يتم تجهيز بيانات CRM الآن."
    >
      <AdminPageSkeleton />
    </AssistantShellChrome>
  );
}

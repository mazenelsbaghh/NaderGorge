'use client';

import { AdminPageSkeleton } from '@/components/admin';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

export default function AssistantChatLoading() {
  return (
    <AssistantShellChrome
      activePath="/assistant/chat"
      sectionLabel="التواصل الداخلي"
      pageTitle="جاري تحميل المحادثات"
      subtitle="يتم تجهيز غرف المحادثة الآن."
    >
      <AdminPageSkeleton />
    </AssistantShellChrome>
  );
}

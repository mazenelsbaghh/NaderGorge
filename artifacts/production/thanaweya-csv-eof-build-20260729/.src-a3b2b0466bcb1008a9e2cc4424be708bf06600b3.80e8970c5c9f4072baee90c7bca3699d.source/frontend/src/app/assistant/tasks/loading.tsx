'use client';

import { AdminPageSkeleton } from '@/components/admin';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

export default function AssistantTasksLoading() {
  return (
    <AssistantShellChrome
      activePath="/assistant/tasks"
      sectionLabel="المهام"
      pageTitle="جاري تحميل المهام"
      subtitle="يتم تجهيز قائمة المهام التشغيلية الآن."
    >
      <AdminPageSkeleton />
    </AssistantShellChrome>
  );
}

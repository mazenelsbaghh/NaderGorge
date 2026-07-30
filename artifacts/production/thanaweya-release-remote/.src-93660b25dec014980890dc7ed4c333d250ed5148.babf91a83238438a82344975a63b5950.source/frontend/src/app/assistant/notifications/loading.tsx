'use client';

import { AdminPageSkeleton } from '@/components/admin';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

export default function AssistantNotificationsLoading() {
  return (
    <AssistantShellChrome
      activePath="/assistant/notifications"
      sectionLabel="التنبيهات"
      pageTitle="جاري تحميل الإشعارات"
      subtitle="يتم تجهيز مركز الإشعارات الآن."
    >
      <AdminPageSkeleton />
    </AssistantShellChrome>
  );
}

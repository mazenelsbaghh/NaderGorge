'use client';

import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { AssistantOperationsTaskBoard } from '@/components/assistant/AssistantOperationsTaskBoard';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function AssistantTasksPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/tasks" permission="tasks.manage">
      <AssistantPage
        activePath="/assistant/tasks"
        sectionLabel="المهام"
        pageTitle="قائمة مهام العمل التشغيلية"
        subtitle="قائمة بالمهام التشغيلية اليومية الموكلة إليك من الإدارة لمتابعتها وإنجازها."
      >
        <AssistantOperationsTaskBoard />
      </AssistantPage>
    </NavRouteGuard>
  );
}

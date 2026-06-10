'use client';

import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { AssistantOperationsTaskBoard } from '@/components/assistant/AssistantOperationsTaskBoard';
import { useHasPermission } from '@/hooks/useHasPermission';
import NotFoundPage from '@/app/not-found';

export default function AssistantTasksPage() {
  const { hasPermission } = useHasPermission();

  if (!hasPermission('tasks.manage')) {
    return <NotFoundPage />;
  }

  return (
    <AssistantShellChrome
      activePath="/assistant/tasks"
      sectionLabel="المهام"
      pageTitle="قائمة مهام العمل التشغيلية"
      subtitle="قائمة بالمهام التشغيلية اليومية الموكلة إليك من الإدارة لمتابعتها وإنجازها."
    >
      <AssistantOperationsTaskBoard />
    </AssistantShellChrome>
  );
}

'use client';

import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { AssistantOperationsTaskBoard } from '@/components/assistant/AssistantOperationsTaskBoard';

export default function AssistantTasksPage() {
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

'use client';

import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { ChatContainer } from '@/components/chat/ChatContainer';
import { useHasPermission } from '@/hooks/useHasPermission';
import NotFoundPage from '@/app/not-found';

export default function AssistantChatPage() {
  const { hasPermission } = useHasPermission();

  if (!hasPermission('chat.manage')) {
    return <NotFoundPage />;
  }

  return (
    <AssistantShellChrome
      activePath="/assistant/chat"
      sectionLabel="التواصل الداخلي"
      pageTitle="غرف المحادثات المشتركة"
      subtitle="تواصل لحظي وتنسيق يومي بين الإدارة والموظفين وفريق الإنتاج."
    >
      <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]">
        <ChatContainer />
      </div>
    </AssistantShellChrome>
  );
}

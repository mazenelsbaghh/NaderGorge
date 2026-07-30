'use client';

import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { ChatContainer } from '@/components/chat/ChatContainer';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function AssistantChatPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/chat" permission="chat.manage">
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
    </NavRouteGuard>
  );
}


'use client';

import { AdminShellChrome } from '@/components/admin';
import { ChatContainer } from '@/components/chat/ChatContainer';
import { AdminGuard } from '@/components/layout/AdminGuard';

export default function AssistantChatPage() {
  return (
    <AdminGuard>
      <AdminShellChrome
        activePath="/assistant/chat"
        sectionLabel="التواصل الداخلي"
        pageTitle="غرف المحادثات المشتركة"
        subtitle="تواصل لحظي وتنسيق يومي بين الإدارة والموظفين وفريق الإنتاج."
      >
        <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]">
          <ChatContainer />
        </div>
      </AdminShellChrome>
    </AdminGuard>
  );
}

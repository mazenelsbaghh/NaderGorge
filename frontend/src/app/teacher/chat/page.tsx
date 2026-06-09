'use client';

import { AdminShellChrome } from '@/components/admin';
import { ChatContainer } from '@/components/chat/ChatContainer';

export default function TeacherChatPage() {
  return (
    <AdminShellChrome
      activePath="/teacher/chat"
      sectionLabel="التواصل الداخلي"
      pageTitle="غرف المحادثات المشتركة"
      subtitle="تواصل لحظي وتنسيق يومي مع الإدارة والمساعدين والمعلمين الآخرين."
    >
      <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]">
        <ChatContainer />
      </div>
    </AdminShellChrome>
  );
}

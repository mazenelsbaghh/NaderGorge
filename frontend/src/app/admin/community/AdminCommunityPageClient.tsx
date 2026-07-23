'use client';

import { MessageSquareText } from 'lucide-react';

import { AdminShellChrome } from '@/components/admin';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { CommunityCommentsModerationTable } from '@/components/admin/CommunityCommentsModerationTable';
import { CommunityPostsModerationTable } from '@/components/admin/CommunityPostsModerationTable';

export default function AdminCommunityPageClient({ mode }: { mode?: 'admin' | 'assistant' }) {
  const Shell = mode === 'assistant' ? AssistantShellChrome : AdminShellChrome;
  const activePath = mode === 'assistant' ? '/assistant/community' : '/admin/community';
  const isAssistantWorkspace = mode === 'assistant';

  return (
    <Shell
      activePath={activePath as any}
      sectionLabel={isAssistantWorkspace ? 'مساحة عمل المجتمع' : 'إدارة المجتمع'}
      pageTitle="مجتمع الطلاب"
      subtitle="مراجعة البوستات المرسلة واعتماد المناسب منها قبل ظهوره للطلاب."
      headerAccessory={
        <div className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary-15)] bg-[var(--admin-primary-10)] px-4 py-2 text-xs font-black tracking-[0.18em] text-[var(--admin-primary)]">
          <MessageSquareText className="h-4 w-4" />
          Community Queue
        </div>
      }
    >
      <div className="space-y-8">
        <CommunityCommentsModerationTable />
        <CommunityPostsModerationTable />
      </div>
    </Shell>
  );
}

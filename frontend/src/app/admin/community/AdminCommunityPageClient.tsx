'use client';

import { MessageSquareText, ExternalLink } from 'lucide-react';
import Link from 'next/link';
import NeumorphButton from '@/components/ui/neumorph-button';

import { AdminShellChrome } from '@/components/admin';
import { CommunityCommentsModerationTable } from '@/components/admin/CommunityCommentsModerationTable';
import { CommunityPostsModerationTable } from '@/components/admin/CommunityPostsModerationTable';

export default function AdminCommunityPageClient() {
  return (
    <AdminShellChrome
      activePath="/admin/community"
      sectionLabel="إدارة المجتمع"
      pageTitle="مجتمع الطلاب"
      subtitle="مراجعة البوستات المرسلة واعتماد المناسب منها قبل ظهوره للطلاب."
      action={
        <Link href="/student/community" prefetch={false} passHref legacyBehavior>
          <NeumorphButton intent="primary" size="lg" pill>
            <ExternalLink className="h-4 w-4 ml-1.5" />
            تصفح مجتمع الطلاب
          </NeumorphButton>
        </Link>
      }
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
    </AdminShellChrome>
  );
}

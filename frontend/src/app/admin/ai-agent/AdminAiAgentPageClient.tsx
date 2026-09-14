'use client';

import { AdminPage } from '@/components/admin';
import { AdminAiAgentWorkspace } from '@/features/admin-ai-agent/AdminAiAgentWorkspace';

export default function AdminAiAgentPageClient() {
  return (
    <AdminPage
      activePath="/admin/ai-agent"
      sectionLabel="لوحة الإدارة"
      pageTitle="وكيل الإدارة AI"
      subtitle="إجابات موثقة وإجراءات لا تُنفّذ قبل تأكيدك الصريح."
    >
      <AdminAiAgentWorkspace />
    </AdminPage>
  );
}

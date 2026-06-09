'use client';

import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { AssistantDashboardTabs } from '@/components/assistant/AssistantDashboardTabs';

export default function AssistantDashboardPage() {
  return (
    <AssistantShellChrome
      activePath="/assistant/dashboard"
      sectionLabel="لوحة التحكم"
      pageTitle="مساحة العمل الأكاديمية والتشغيلية"
      subtitle="إدارة مهام الطلاب والعمليات اليومية المسندة إليك ومتابعة الأداء أولاً بأول."
    >
      <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
        <AssistantDashboardTabs />
      </div>
    </AssistantShellChrome>
  );
}

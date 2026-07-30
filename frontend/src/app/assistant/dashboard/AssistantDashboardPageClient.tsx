'use client';

import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { AssistantDashboardTabs } from '@/components/assistant/AssistantDashboardTabs';
import { AttendanceWorkspace } from '@/features/hr/attendance';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function AssistantDashboardPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/dashboard">
      <AssistantPage
        activePath="/assistant/dashboard"
        sectionLabel="لوحة التحكم"
        pageTitle="مساحة عملك اليومية"
        subtitle="سجّل حضورك أولًا، ثم انتقل إلى مهام الطلاب والعمليات اليومية المسندة إليك."
      >
        <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
          <section aria-label="تسجيل الحضور اليوم">
            <AttendanceWorkspace />
          </section>
          <AssistantDashboardTabs />
        </div>
      </AssistantPage>
    </NavRouteGuard>
  );
}

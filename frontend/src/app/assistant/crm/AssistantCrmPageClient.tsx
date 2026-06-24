'use client';

import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { CrmStudentQueue } from '@/components/crm/CrmStudentQueue';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function AssistantCrmPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/crm" permission="crm.manage">
      <AssistantShellChrome
        activePath="/assistant/crm"
        sectionLabel="متابعة الطلاب"
        pageTitle="قائمة الاتصال اليومية"
        subtitle="متابعة الطلاب الموكلين إليك، تسجيل المكالمات، وتحديث الحالات أولاً بأول."
      >
        <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
          <CrmStudentQueue mode="agent" />
        </div>
      </AssistantShellChrome>
    </NavRouteGuard>
  );
}


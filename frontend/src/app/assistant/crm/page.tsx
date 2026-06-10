'use client';

import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { CrmStudentQueue } from '@/components/crm/CrmStudentQueue';
import { useHasPermission } from '@/hooks/useHasPermission';
import NotFoundPage from '@/app/not-found';

export default function AssistantCrmPage() {
  const { hasPermission } = useHasPermission();

  if (!hasPermission('crm.manage')) {
    return <NotFoundPage />;
  }

  return (
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
  );
}

'use client';

import { AdminShellChrome } from '@/components/admin';
import { CrmStudentQueue } from '@/components/crm/CrmStudentQueue';
import { AdminGuard } from '@/components/layout/AdminGuard';

export default function AssistantCrmPage() {
  return (
    <AdminGuard>
      <AdminShellChrome
        activePath="/assistant/crm"
        sectionLabel="متابعة الطلاب"
        pageTitle="قائمة الاتصال اليومية"
        subtitle="متابعة الطلاب الموكلين إليك، تسجيل المكالمات، وتحديث الحالات أولاً بأول."
      >
        <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
          <CrmStudentQueue mode="agent" />
        </div>
      </AdminShellChrome>
    </AdminGuard>
  );
}

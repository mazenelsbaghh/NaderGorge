'use client';

import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import DesktopStudentsWorkspace from '@/app/admin/center-desktop/DesktopStudentsWorkspace';

export default function AssistantCenterStudentsPage() {
  return <NavRouteGuard routePath="/assistant/center-students" permission="center_students.view">
    <AssistantPage activePath="/assistant/center-students" sectionLabel="خدمة الطلاب" pageTitle="طلاب السنتر" subtitle="ابحث عن الطالب وافتح سجله ونزّل تقرير PDF.">
      <DesktopStudentsWorkspace staff />
    </AssistantPage>
  </NavRouteGuard>;
}

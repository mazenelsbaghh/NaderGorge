'use client';

import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import AdminQuestionsPageClient from '@/app/admin/questions/AdminQuestionsPageClient';

export default function AssistantQuestionsPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/questions" permission="exams.manage">
      <AdminQuestionsPageClient mode="assistant" />
    </NavRouteGuard>
  );
}

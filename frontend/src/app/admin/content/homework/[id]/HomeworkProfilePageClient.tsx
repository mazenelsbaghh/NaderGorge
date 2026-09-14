'use client';

import { AttachedHomeworkViewer, AdminBackButton, AdminPage } from '@/components/admin';

export default function HomeworkProfilePageClient({ id }: { id: string }) {
  return (
    <AdminPage
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ بروفايل الواجب"
      pageTitle="بروفايل الواجب"
      subtitle="مراجعة بيانات الواجب وأسئلته وتسليمات الطلاب"
      action={<AdminBackButton />}
    >
      <AttachedHomeworkViewer homeworkId={id} />
    </AdminPage>
  );
}

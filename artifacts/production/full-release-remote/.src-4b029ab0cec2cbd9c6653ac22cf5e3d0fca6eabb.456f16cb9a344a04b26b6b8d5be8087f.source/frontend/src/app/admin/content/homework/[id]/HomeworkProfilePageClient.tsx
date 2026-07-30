'use client';

import { AttachedHomeworkViewer, AdminBackButton, AdminShellChrome } from '@/components/admin';

export default function HomeworkProfilePageClient({ id }: { id: string }) {
  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ بروفايل الواجب"
      pageTitle="بروفايل الواجب"
      subtitle="مراجعة بيانات الواجب وأسئلته وتسليمات الطلاب"
      action={<AdminBackButton />}
    >
      <AttachedHomeworkViewer homeworkId={id} />
    </AdminShellChrome>
  );
}

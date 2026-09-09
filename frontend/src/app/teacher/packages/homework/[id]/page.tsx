import { AttachedHomeworkViewer } from '@/components/admin/AttachedHomeworkViewer';
import { TeacherShellChrome } from '@/components/teacher/TeacherShellChrome';

export default async function TeacherHomeworkProfile({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return (
    <TeacherShellChrome activePath="/teacher/packages" sectionLabel="المحتوى الدراسي ▸ بروفايل الواجب" pageTitle="بروفايل الواجب" subtitle="الأسئلة والمعاينة وتسليمات الطلاب">
      <AttachedHomeworkViewer homeworkId={id} surface="teacher" />
    </TeacherShellChrome>
  );
}

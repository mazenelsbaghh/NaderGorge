import ExamProfilePageClient from '@/app/admin/content/exams/[id]/ExamProfilePageClient';

export default async function TeacherExamProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <ExamProfilePageClient id={resolvedParams.id} surface="teacher" />;
}

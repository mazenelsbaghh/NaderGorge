import AddExamQuestionPageClient from '@/app/admin/content/exams/[id]/add-question/AddExamQuestionPageClient';

export default async function TeacherAddExamQuestionPage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <AddExamQuestionPageClient params={resolvedParams} surface="teacher" />;
}

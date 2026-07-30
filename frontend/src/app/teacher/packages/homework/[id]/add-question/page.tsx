import AddHomeworkQuestionPageClient from '@/app/admin/content/homework/[id]/add-question/AddHomeworkQuestionPageClient';

export default async function TeacherAddHomeworkQuestionPage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <AddHomeworkQuestionPageClient params={resolvedParams} surface="teacher" />;
}

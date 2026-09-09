import AddExamQuestionPageClient from '@/app/admin/content/exams/[id]/add-question/AddExamQuestionPageClient';

export default async function TeacherAddExamQuestionPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ question?: string }> }) {
  const resolvedParams = await params;
  const { question } = await searchParams;
  return <AddExamQuestionPageClient params={resolvedParams} surface="teacher" initialQuestionId={question} />;
}

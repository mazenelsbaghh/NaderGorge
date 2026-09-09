import AddHomeworkQuestionPageClient from '@/app/admin/content/homework/[id]/add-question/AddHomeworkQuestionPageClient';

export default async function TeacherAddHomeworkQuestionPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ question?: string }> }) {
  const resolvedParams = await params;
  const { question } = await searchParams;
  return <AddHomeworkQuestionPageClient params={resolvedParams} surface="teacher" initialQuestionId={question} />;
}

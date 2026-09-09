import AddHomeworkQuestionPageClient from './AddHomeworkQuestionPageClient';

export default async function Page({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ question?: string }> }) {
  const resolvedParams = await params;
  const { question } = await searchParams;
  return <AddHomeworkQuestionPageClient params={resolvedParams} initialQuestionId={question} />;
}

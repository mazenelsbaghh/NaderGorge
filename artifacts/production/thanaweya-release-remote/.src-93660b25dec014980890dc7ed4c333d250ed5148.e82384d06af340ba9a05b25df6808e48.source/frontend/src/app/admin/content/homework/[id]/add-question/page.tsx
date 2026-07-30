import AddHomeworkQuestionPageClient from './AddHomeworkQuestionPageClient';

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <AddHomeworkQuestionPageClient params={resolvedParams} />;
}

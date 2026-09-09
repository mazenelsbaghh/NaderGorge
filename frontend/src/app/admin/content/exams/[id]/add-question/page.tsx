import AddExamQuestionPageClient from "./AddExamQuestionPageClient";

export default async function AddExamQuestionPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ question?: string }> }) {
  const resolvedParams = await params;
  const { question } = await searchParams;
  return <AddExamQuestionPageClient params={resolvedParams} initialQuestionId={question} />;
}

import AddExamQuestionPageClient from "./AddExamQuestionPageClient";

export default async function AddExamQuestionPage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <AddExamQuestionPageClient params={resolvedParams} />;
}

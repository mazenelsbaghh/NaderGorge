import ExamProfilePageClient from "./ExamProfilePageClient";

export default async function ExamProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <ExamProfilePageClient id={resolvedParams.id} />;
}

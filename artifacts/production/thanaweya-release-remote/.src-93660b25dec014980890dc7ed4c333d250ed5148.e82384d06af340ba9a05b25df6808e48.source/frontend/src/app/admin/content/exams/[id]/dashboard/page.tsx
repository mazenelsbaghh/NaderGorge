import ExamDashboardPageClient from "./ExamDashboardPageClient";

export default async function ExamDashboardPage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <ExamDashboardPageClient params={resolvedParams} />;
}

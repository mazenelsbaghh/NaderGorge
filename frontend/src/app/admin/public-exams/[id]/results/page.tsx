import AdminPublicExamResultsPageClient from './PublicExamResultsPageClient';

export default async function AdminPublicExamResultsPage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <AdminPublicExamResultsPageClient productId={resolvedParams.id} />;
}

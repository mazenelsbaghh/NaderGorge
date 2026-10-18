import AdminPublicExamResultsPageClient from '@/app/admin/public-exams/[id]/results/PublicExamResultsPageClient';

export default async function StaffPublicExamResultsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <AdminPublicExamResultsPageClient productId={id} />;
}

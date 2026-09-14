import AdminPublicExamProfilePageClient from './PublicExamProfilePageClient';

export default async function AdminPublicExamProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <AdminPublicExamProfilePageClient productId={resolvedParams.id} />;
}

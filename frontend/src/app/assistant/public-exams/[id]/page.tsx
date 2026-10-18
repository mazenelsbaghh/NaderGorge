import AdminPublicExamProfilePageClient from '@/app/admin/public-exams/[id]/PublicExamProfilePageClient';

export default async function StaffPublicExamProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <AdminPublicExamProfilePageClient productId={id} />;
}

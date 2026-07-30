import AdminStudentProfileClient from '@/app/admin/users/[id]/AdminStudentProfileClient';

export default async function AssistantStudentProfilePage({ params }: { params: Promise<{ id: string }> }) {
  return <AdminStudentProfileClient params={await params} staff />;
}

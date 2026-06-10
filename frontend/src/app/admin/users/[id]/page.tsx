import AdminStudentProfileClient from "./AdminStudentProfileClient";

export default async function AdminStudentProfile({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <AdminStudentProfileClient params={resolvedParams} />;
}

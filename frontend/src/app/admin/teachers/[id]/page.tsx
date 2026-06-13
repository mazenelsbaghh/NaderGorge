import TeacherProfilePageClient from "./TeacherProfilePageClient";

export default async function AdminTeacherProfile({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <TeacherProfilePageClient params={resolvedParams} />;
}

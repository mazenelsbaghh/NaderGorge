import TeacherTermProfilePageClient from "./TeacherTermProfilePageClient";

export default async function TeacherTermProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <TeacherTermProfilePageClient params={resolvedParams} />;
}

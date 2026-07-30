import TeacherSectionProfilePageClient from "./TeacherSectionProfilePageClient";

export default async function TeacherSectionProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <TeacherSectionProfilePageClient params={resolvedParams} />;
}

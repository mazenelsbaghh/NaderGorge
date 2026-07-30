import TeacherLessonProfilePageClient from "./TeacherLessonProfilePageClient";

export default async function TeacherLessonProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <TeacherLessonProfilePageClient params={resolvedParams} />;
}

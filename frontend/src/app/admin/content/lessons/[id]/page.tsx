import LessonProfilePageClient from "./LessonProfilePageClient";

export default async function LessonProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <LessonProfilePageClient params={resolvedParams} />;
}

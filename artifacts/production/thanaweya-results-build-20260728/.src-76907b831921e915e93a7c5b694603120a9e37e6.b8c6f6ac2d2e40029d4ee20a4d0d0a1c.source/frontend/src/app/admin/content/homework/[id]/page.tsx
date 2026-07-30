import HomeworkProfilePageClient from './HomeworkProfilePageClient';

export default async function HomeworkProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <HomeworkProfilePageClient id={resolvedParams.id} />;
}

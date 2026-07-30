import TeacherPublicProfilePageClient from './TeacherPublicProfilePageClient';

export default async function TeacherPublicProfilePage({ params }: { params: Promise<{ teacherId: string }> }) {
  const { teacherId } = await params;
  return <TeacherPublicProfilePageClient teacherId={teacherId} />;
}

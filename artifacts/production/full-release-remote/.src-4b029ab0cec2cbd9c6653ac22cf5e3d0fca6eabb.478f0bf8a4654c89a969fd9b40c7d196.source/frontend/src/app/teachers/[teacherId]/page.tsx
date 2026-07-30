import TeacherPublicProfilePageClient from '../../student/teachers/[teacherId]/TeacherPublicProfilePageClient';

export default async function PublicTeacherProfilePage({ params }: { params: Promise<{ teacherId: string }> }) {
  const { teacherId } = await params;
  return <TeacherPublicProfilePageClient teacherId={teacherId} visitor />;
}

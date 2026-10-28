import TeacherStudentProfileClient from './TeacherStudentProfileClient';

export default async function TeacherStudentProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <TeacherStudentProfileClient studentId={id} />;
}

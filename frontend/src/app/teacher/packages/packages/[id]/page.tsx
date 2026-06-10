import TeacherPackageProfilePageClient from "./TeacherPackageProfilePageClient";

export default async function TeacherPackageProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <TeacherPackageProfilePageClient params={resolvedParams} />;
}

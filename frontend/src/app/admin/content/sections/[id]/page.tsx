import SectionProfilePageClient from "./SectionProfilePageClient";

export default async function SectionProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <SectionProfilePageClient params={resolvedParams} />;
}

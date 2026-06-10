import SubmissionsPageClient from "./SubmissionsPageClient";

export default async function SubmissionsPage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <SubmissionsPageClient params={resolvedParams} />;
}

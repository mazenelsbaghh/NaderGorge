import TermProfilePageClient from "./TermProfilePageClient";

export default async function TermProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <TermProfilePageClient params={resolvedParams} />;
}

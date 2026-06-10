import EditFormPageClient from "./EditFormPageClient";

export default async function EditFormPage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <EditFormPageClient params={resolvedParams} />;
}

import PublicFormPageClient from "./PublicFormPageClient";

export default async function PublicFormPage({ params }: { params: Promise<{ slug: string }> }) {
  const resolvedParams = await params;
  return <PublicFormPageClient params={resolvedParams} />;
}

import PackageProfilePageClient from "./PackageProfilePageClient";

export default async function PackageProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = await params;
  return <PackageProfilePageClient params={resolvedParams} />;
}

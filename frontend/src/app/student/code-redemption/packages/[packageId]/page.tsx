import PackageCodeRedemptionPageClient from "./PackageCodeRedemptionPageClient";

export default async function PackageCodeRedemptionPage({ params }: { params: Promise<{ packageId: string }> }) {
  const resolvedParams = await params;
  return <PackageCodeRedemptionPageClient params={resolvedParams} />;
}

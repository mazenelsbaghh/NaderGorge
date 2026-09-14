import PackageCodeRedemptionPageClient from './PackageCodeRedemptionPageClient';

export default async function PackageCodeRedemptionPage({ params }: { params: Promise<{ packageId: string }> }) {
  return <PackageCodeRedemptionPageClient params={await params} />;
}

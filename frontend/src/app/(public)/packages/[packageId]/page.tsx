import PublicPackagePageClient from './PublicPackagePageClient';

export default async function PublicPackagePage({
  params,
}: {
  params: Promise<{ packageId: string }>;
}) {
  const { packageId } = await params;
  return <PublicPackagePageClient packageId={packageId} />;
}

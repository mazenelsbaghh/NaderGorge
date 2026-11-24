import RechargeRecommendationsPageClient from './RechargeRecommendationsPageClient';

export default async function RechargeRecommendationsPage({ params }: { params: Promise<{ redemptionId: string }> }) {
  const { redemptionId } = await params;
  return <RechargeRecommendationsPageClient redemptionId={redemptionId} />;
}

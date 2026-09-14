import CouponProfilePageClient from './CouponProfilePageClient';

type PageProps = {
  params: Promise<{ couponId: string }>;
};

export default async function CouponProfilePage({ params }: PageProps) {
  const { couponId } = await params;
  return <CouponProfilePageClient couponId={couponId} />;
}

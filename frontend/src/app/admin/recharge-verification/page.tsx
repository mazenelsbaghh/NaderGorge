import { Metadata } from 'next';
import RechargeVerificationPageClient from './RechargeVerificationPageClient';

export const metadata: Metadata = {
  title: 'مراجعة طلبات الشحن والمدفوعات | منصة نادر جورج الأكاديمية',
  description: 'مراجعة طلبات الشحن اليدوية ومطابقتها برسائل تأكيد فودافون كاش.',
};

export default function AdminRechargeVerificationPage() {
  return <RechargeVerificationPageClient />;
}

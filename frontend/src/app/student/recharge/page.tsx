import { Metadata } from 'next';
import StudentRechargePageClient from './StudentRechargePageClient';

export const metadata: Metadata = {
  title: 'شحن المحفظة بالتحويل الرقمي | منصة نادر جورج الأكاديمية',
  description: 'قم بشحن رصيدك عن طريق تحويل فودافون كاش ومطابقته تلقائياً.',
};

export default function StudentRechargePage() {
  return <StudentRechargePageClient />;
}

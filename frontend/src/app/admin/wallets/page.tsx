import { Metadata } from 'next';
import AdminWalletsPageClient from './AdminWalletsPageClient';

export const metadata: Metadata = {
  title: 'إدارة المحافظ الرقمية | منصة نادر جورج الأكاديمية',
  description: 'إدارة وتتبع المحافظ الرقمية وربط الهواتف الذكية لمطابقة المدفوعات آلياً.',
};

export default function AdminWalletsPage() {
  return <AdminWalletsPageClient />;
}

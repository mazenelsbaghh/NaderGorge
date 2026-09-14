import { AdminPageSkeleton } from '@/components/admin';

export default function AssistantDashboardLoading() {
  return <div role="status" aria-label="جاري تحميل لوحة التحكم" aria-busy="true"><AdminPageSkeleton /></div>;
}

import { AdminPageSkeleton } from '@/components/admin';

export default function AssistantVacationsLoading() {
  return <div role="status" aria-label="جاري تحميل طلبات الإجازة" aria-busy="true"><AdminPageSkeleton /></div>;
}

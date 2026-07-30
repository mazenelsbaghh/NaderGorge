import { AdminPageSkeleton } from '@/components/admin';

export default function AssistantTasksLoading() {
  return <div role="status" aria-label="جاري تحميل المهام" aria-busy="true"><AdminPageSkeleton /></div>;
}

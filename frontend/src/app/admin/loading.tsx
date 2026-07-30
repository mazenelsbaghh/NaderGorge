import { AdminPageSkeleton } from '@/components/admin';
import { AsyncRegionState } from '@/components/ui/AsyncRegionState';

export default function AdminLoading() {
  return (
    <AsyncRegionState
      status="loading"
      message="جاري تحميل محتوى الإدارة"
    >
      <AdminPageSkeleton />
    </AsyncRegionState>
  );
}

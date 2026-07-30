import { AdminPageSkeleton } from '@/components/admin';
import { AsyncRegionState } from '@/components/ui/AsyncRegionState';

export default function AssistantLoading() {
  return (
    <AsyncRegionState
      status="loading"
      message="جاري تحميل محتوى المساعد"
    >
      <AdminPageSkeleton />
    </AsyncRegionState>
  );
}

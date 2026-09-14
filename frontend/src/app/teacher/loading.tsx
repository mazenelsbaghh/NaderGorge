import { AdminPageSkeleton } from '@/components/admin';
import { AsyncRegionState } from '@/components/ui/AsyncRegionState';

export default function TeacherLoading() {
  return (
    <AsyncRegionState
      status="loading"
      message="جاري تحميل محتوى المعلم"
    >
      <AdminPageSkeleton />
    </AsyncRegionState>
  );
}

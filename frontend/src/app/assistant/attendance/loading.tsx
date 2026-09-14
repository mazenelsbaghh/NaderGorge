import { AdminPageSkeleton } from '@/components/admin';

export default function AssistantAttendanceLoading() {
  return <div role="status" aria-label="جاري تحميل سجل الحضور" aria-busy="true"><AdminPageSkeleton /></div>;
}

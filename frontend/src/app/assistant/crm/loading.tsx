import { AdminPageSkeleton } from '@/components/admin';

export default function AssistantCrmLoading() {
  return <div role="status" aria-label="جاري تحميل قائمة الاتصال" aria-busy="true"><AdminPageSkeleton /></div>;
}

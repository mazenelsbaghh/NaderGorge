import { AdminPageSkeleton } from '@/components/admin';

export default function AssistantNotificationsLoading() {
  return <div role="status" aria-label="جاري تحميل الإشعارات" aria-busy="true"><AdminPageSkeleton /></div>;
}

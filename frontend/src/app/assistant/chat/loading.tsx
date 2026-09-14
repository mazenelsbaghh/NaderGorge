import { AdminPageSkeleton } from '@/components/admin';

export default function AssistantChatLoading() {
  return <div role="status" aria-label="جاري تحميل المحادثات" aria-busy="true"><AdminPageSkeleton /></div>;
}

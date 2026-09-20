import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import RefundManager from '@/components/admin/platform-finance/RefundManager';

export default function AssistantRefundsPage() {
  return <AssistantShellChrome activePath="/assistant/refunds" sectionLabel="المالية" pageTitle="استردادات الطلاب" subtitle="تسجيل المبلغ المرتجع وإلغاء اشتراك الطالب."><RefundManager staff /></AssistantShellChrome>;
}

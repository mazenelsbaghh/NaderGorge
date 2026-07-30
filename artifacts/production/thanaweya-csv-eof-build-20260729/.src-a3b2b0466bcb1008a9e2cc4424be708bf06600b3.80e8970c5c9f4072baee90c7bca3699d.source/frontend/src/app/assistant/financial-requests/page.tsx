import { FinancialRequestsWorkspace } from '@/features/hr/financial-requests';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

export default function AssistantFinancialRequestsPage() {
  return (
    <AssistantShellChrome
      activePath="/assistant/financial-requests"
      sectionLabel="شؤون الموظف"
      pageTitle="الطلبات المالية"
      subtitle="قدّم طلب سلفة أو قرض أو مصروف، وتابع حالة الاعتماد وجدول السداد."
    >
      <FinancialRequestsWorkspace />
    </AssistantShellChrome>
  );
}

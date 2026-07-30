import { FinancialRequestsWorkspace } from '@/features/hr/financial-requests';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';

export default function AssistantFinancialRequestsPage() {
  return (
    <AssistantPage
      activePath="/assistant/financial-requests"
      sectionLabel="شؤون الموظف"
      pageTitle="الطلبات المالية"
      subtitle="قدّم طلب سلفة أو قرض أو مصروف، وتابع حالة الاعتماد وجدول السداد."
    >
      <FinancialRequestsWorkspace />
    </AssistantPage>
  );
}

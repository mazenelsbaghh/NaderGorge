import { PayslipWorkspace } from '@/features/hr/payroll';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';

export default function AssistantPayrollPage() {
  return (
    <AssistantPage
      activePath="/assistant/payroll"
      sectionLabel="شؤون الموظف"
      pageTitle="كشوف الرواتب"
      subtitle="راجع صافي راتبك والاستحقاقات والاستقطاعات وشرح طريقة حساب كل بند."
    >
      <PayslipWorkspace />
    </AssistantPage>
  );
}

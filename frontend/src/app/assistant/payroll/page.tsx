import { PayslipWorkspace } from '@/features/hr/payroll';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function AssistantPayrollPage() {
  return (
    <NavRouteGuard routePath="/assistant/payroll"><AssistantPage
      activePath="/assistant/payroll"
      sectionLabel="شؤون الموظف"
      pageTitle="كشوف الرواتب"
      subtitle="راجع صافي راتبك والاستحقاقات والاستقطاعات وشرح طريقة حساب كل بند."
    >
      <PayslipWorkspace />
    </AssistantPage></NavRouteGuard>
  );
}

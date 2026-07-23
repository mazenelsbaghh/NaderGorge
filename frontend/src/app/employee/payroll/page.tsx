import { PayslipWorkspace } from '@/features/hr/payroll';
import { EmployeePageShell } from '@/features/hr/components/EmployeePageShell';

export default function EmployeePayrollPage() {
  return (
    <EmployeePageShell
      compact
      title="كشوف الرواتب"
      description="راجع صافي راتبك والاستحقاقات والاستقطاعات وشرح طريقة حساب كل بند."
    >
      <PayslipWorkspace />
    </EmployeePageShell>
  );
}

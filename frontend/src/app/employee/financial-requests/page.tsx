import { FinancialRequestsWorkspace } from '@/features/hr/financial-requests';
import { EmployeePageShell } from '@/features/hr/components/EmployeePageShell';

export default function EmployeeFinancialRequestsPage() {
  return (
    <EmployeePageShell
      compact
      title="الطلبات المالية"
      description="قدّم طلب سلفة أو قرض أو مصروف، وتابع حالة الاعتماد وجدول السداد."
    >
      <FinancialRequestsWorkspace />
    </EmployeePageShell>
  );
}

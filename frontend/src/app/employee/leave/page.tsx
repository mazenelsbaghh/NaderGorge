import { LeaveWorkspace } from '@/features/hr/leave';
import { EmployeePageShell } from '@/features/hr/components/EmployeePageShell';

export default function EmployeeLeavePage() {
  return (
    <EmployeePageShell
      compact
      title="الإجازات والأرصدة"
      description="تابع رصيدك، قدّم طلبًا جديدًا، واعرف حالة الاعتماد من مكان واحد."
    >
      <LeaveWorkspace />
    </EmployeePageShell>
  );
}

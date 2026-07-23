import { AttendanceWorkspace } from '@/features/hr/attendance';
import { EmployeePageShell } from '@/features/hr/components/EmployeePageShell';

export default function EmployeeAttendancePage() {
  return (
    <EmployeePageShell
      compact
      title="الحضور والانصراف"
      description="سجّل يومك، تابع ساعات العمل، وقدّم طلب تصحيح عند الحاجة."
    >
      <AttendanceWorkspace />
    </EmployeePageShell>
  );
}

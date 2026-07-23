import { EmployeeHub } from '@/features/hr/lifecycle';
import { EmployeePageShell } from '@/features/hr/components/EmployeePageShell';

export default function EmployeeHomePage() {
  return (
    <EmployeePageShell
      home
      title="ملفي وخدماتي"
      description="الحضور والإجازات والراتب والطلبات والمستندات والعُهد في مكان واحد."
    >
      <EmployeeHub />
    </EmployeePageShell>
  );
}

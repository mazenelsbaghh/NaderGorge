import HrEmployeeProfileClient from './HrEmployeeProfileClient';

export default async function HrEmployeeProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <HrEmployeeProfileClient employeeId={id} />;
}

import type { TeacherLedgerLine } from './types';
import { teacherMoney } from './TeacherAccountOverview';

export function allocationRuleLabel(mode: string, value: number, agreementMode?: string | null) {
  if (mode === 'Reversal') return 'خصم مرتجع من نصيب المدرّس';
  if (agreementMode === 'PlatformFixedPerUnit') return `نصيب المنصّة ثابت: ${teacherMoney(value)}`;
  if (mode === 'Percentage' || mode === 'CommissionRate') return `${value}% للمدرّس`;
  if (mode === 'ManualCompensation') return 'تعويض مسجل';
  return `نصيب ثابت: ${teacherMoney(value)}`;
}

export function TeacherAllocationExplanation({ line }: { line: TeacherLedgerLine }) {
  return <details className="mt-2 max-w-80 whitespace-normal text-xs font-normal leading-6">
    <summary className="cursor-pointer py-2 text-[var(--admin-primary)]">اتحسب إزاي؟</summary>
    <p>{allocationRuleLabel(line.allocationMode, line.allocationValue, line.agreementAllocationMode)}</p>
    <p>أساس الحساب وقت العملية: {teacherMoney(line.grossBasisAmount)}{line.priceBasis ? ` (${line.priceBasis === 'Gross' ? 'قبل الخصم' : 'بعد الخصم'})` : ''}.</p>
    <p>نصيب المدرّس المسجل: {teacherMoney(line.teacherShareAmount)}. نصيب المنصّة: {teacherMoney(line.platformShareAmount)}.</p>
    <p className="text-[var(--admin-muted)]">{line.agreementId ? 'طبقًا للاتفاق المحفوظ مع العملية.' : 'طبقًا للقاعدة المحفوظة وقت العملية.'} المبلغ المسجل يشمل توزيع الخصم والتقريب.</p>
  </details>;
}

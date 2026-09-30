import Link from 'next/link';
import type { ProfitTeacherRow } from '@/services/platform-profits-service';
import { financeMoney as money } from './FinanceProfitSummary';

export default function TeacherProfitList({ rows }: { rows: ProfitTeacherRow[] }) {
  return <div className="overflow-x-auto"><table className="w-full min-w-[760px] text-right text-sm">
    <caption className="sr-only">مبيعات المدرسين ونصيب المنصة في الفترة المختارة</caption>
    <thead><tr>{['المدرس', 'طلاب اشتروا', 'المبيعات', 'نصيبه', 'نصيب المنصة', 'باقي له الآن'].map(label => <th key={label} scope="col" className="whitespace-nowrap px-3 py-4">{label}</th>)}</tr></thead>
    <tbody className="divide-y divide-[var(--admin-border)]">{rows.map(({ period, account, purchasingStudents, purchaseOperations, reconciliationDifference }) => <tr key={period.teacherId}>
      <th scope="row" className="px-3 py-4"><Link className="inline-flex min-h-11 items-center font-bold text-[var(--admin-primary)] underline underline-offset-4" href={`/admin/teachers/${period.teacherId}/account`}>{period.teacherName}</Link>{Math.abs(reconciliationDifference) >= 0.01 && <span className="block text-xs font-normal text-[var(--admin-muted)]">الرصيد محتاج مراجعة</span>}</th>
      <td className="px-3 py-4">{purchasingStudents}<span className="block text-xs text-[var(--admin-muted)]">{purchaseOperations} عملية شراء</span></td>
      <td className="whitespace-nowrap px-3 py-4 tabular-nums">{money(period.grossSales - period.refunds)}</td>
      <td className="whitespace-nowrap px-3 py-4 tabular-nums">{money(period.teacherShare)}</td>
      <td className="whitespace-nowrap px-3 py-4 tabular-nums">{money(period.platformShare)}</td>
      <td className="whitespace-nowrap px-3 py-4 font-bold tabular-nums">{account ? money(account.netPayable) : 'غير متاح'}{account && account.netBalance < 0 && <span className="block text-xs font-normal text-[var(--admin-muted)]">مطلوب منه {money(-account.netBalance)}</span>}{!!account?.reserved && <span className="block text-xs font-normal text-[var(--admin-muted)]">ومحجوز للصرف {money(account.reserved)}</span>}{!!account?.codeAmountDue && <span className="block text-xs font-normal text-[var(--admin-muted)]">عليه من الأكواد {money(account.codeAmountDue)}</span>}</td>
    </tr>)}</tbody>
  </table></div>;
}

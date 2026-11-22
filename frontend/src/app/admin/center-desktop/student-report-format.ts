import type { DesktopStudentLesson } from '@/services/center-desktop-service';

const attendanceLabels = new Map([['present', 'حاضر'], ['absent', 'غائب'], ['makeup', 'تعويض']]);
const homeworkLabels = new Map([['complete', 'عمل الواجب'], ['incomplete', 'الواجب ناقص'], ['missing', 'لم يعمل'], ['exempt', 'معفى'], ['notReviewed', 'لم يُراجع']]);
export const attendanceLabel = (status?: string) => attendanceLabels.get(status ?? '') ?? 'لم يُسجل';
export const homeworkLabel = (status: string) => homeworkLabels.get(status) ?? 'لم يُراجع';
export const reportMoney = (value: number) => `${new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 }).format(value / 100)} ج`;
export function examScore(exam: DesktopStudentLesson['exams'][number]) {
  return exam.absent ? 'غائب عن الامتحان' : exam.score == null ? 'لم تُرصد الدرجة' : `${exam.score} من ${exam.maxScore ?? 'نهاية غير معروفة'}`;
}

export function paymentLines(payment: DesktopStudentLesson['payment']) {
  const lines: string[] = [];
  if (payment.coverage === 'package') lines.push(`مشمولة في باقة ${payment.packageName || 'الشهر'}`);
  if (payment.coverage === 'pending') lines.push('بانتظار الدفع');
  for (const receipt of payment.receipts) {
    lines.push(`${payment.coverage === 'package' ? 'دفع الباقة' : receipt.description || 'دفع الحصة'}: ${receipt.amount == null ? 'غير متوفر' : reportMoney(receipt.amount + receipt.settled)}`);
    if (receipt.expected != null && receipt.amount != null && receipt.expected > receipt.amount + receipt.settled) lines.push(`متبقي ${reportMoney(receipt.expected - receipt.amount - receipt.settled)}`);
    if (receipt.refunded > 0) lines.push(`مرتجع: ${reportMoney(receipt.refunded)}`);
    if (receipt.online > 0) lines.push(`محول للأونلاين: ${reportMoney(receipt.online)}`);
  }
  if (payment.centerFeePaid > 0) lines.push(`رسوم السنتر: ${reportMoney(payment.centerFeePaid)}`);
  if (!lines.length) lines.push('لم يُسجل دفع');
  return lines;
}

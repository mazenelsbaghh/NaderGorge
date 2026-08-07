'use client';

import Link from 'next/link';
import { FormEvent, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { adminService, type AdminUserListDto, type StudentProfileExtendedDto } from '@/services/admin-service';
import platformFinanceService, { type FinanceBootstrap, type PlatformRefundRow } from '@/services/platform-finance-service';

const money = (value: number) => `${new Intl.NumberFormat('ar-EG', { minimumFractionDigits: 2 }).format(value)} ج.م`;

export default function RefundManager() {
  const [rows, setRows] = useState<PlatformRefundRow[]>([]);
  const [bootstrap, setBootstrap] = useState<FinanceBootstrap | null>(null);
  const [error, setError] = useState('');
  const [phone, setPhone] = useState('');
  const [students, setStudents] = useState<AdminUserListDto[]>([]);
  const [student, setStudent] = useState<StudentProfileExtendedDto | null>(null);
  const [grantId, setGrantId] = useState('');
  const [treasuryId, setTreasuryId] = useState('');
  const [refundAmount, setRefundAmount] = useState('');
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const load = async () => {
    try {
      const [refunds, financeBootstrap] = await Promise.all([
        platformFinanceService.getRefunds(),
        platformFinanceService.bootstrap(),
      ]);
      setRows(refunds);
      setBootstrap(financeBootstrap);
    } catch {
      setError('تعذر تحميل الاستردادات');
    }
  };

  useEffect(() => { void load(); }, []);

  async function searchStudent() {
    const result = await adminService.listUsers(1, 10, phone.trim(), undefined, undefined, undefined, undefined, undefined, 'Student');
    setStudents(result?.items || []);
  }

  async function selectStudent(userId: string) {
    setStudent(await adminService.getStudentProfile(userId));
    setGrantId('');
    setStudents([]);
  }

  async function createExternalRefund(event: FormEvent) {
    event.preventDefault();
    const selectedPackage = student?.packages.find(item => item.accessGrantId === grantId && item.isActive);
    const amount = Number(refundAmount);
    if (!student || !selectedPackage?.purchaseOperationId || !treasuryId || !reason.trim() || amount <= 0 || amount > selectedPackage.paidAmount) return;
    const teacherRatio = selectedPackage.paidAmount > 0 ? selectedPackage.teacherShareAmount / selectedPackage.paidAmount : 0;
    const teacherAmount = Math.min(amount, Math.max(0, Number((amount * teacherRatio).toFixed(2))));
    setSubmitting(true);
    try {
      await platformFinanceService.createExternalPackageRefund({
        accessGrantId: selectedPackage.accessGrantId,
        purchaseOperationId: selectedPackage.purchaseOperationId,
        studentId: student.id,
        teacherId: selectedPackage.teacherId || undefined,
        platformAmount: amount - teacherAmount,
        teacherAmount,
        treasuryAccountId: treasuryId,
        reason: reason.trim(),
      });
      toast.success('تم إلغاء الباقة وتسجيل الاسترداد الخارجي في المركز المالي');
      setStudent(await adminService.getStudentProfile(student.id));
      setGrantId('');
      setRefundAmount('');
      setReason('');
      await load();
    } catch (caught: any) {
      toast.error(caught?.response?.data?.message || 'تعذر تنفيذ الاسترداد');
    } finally {
      setSubmitting(false);
    }
  }

  async function reverse(id: string) {
    const reversalReason = window.prompt('سبب عكس الاسترداد؟');
    if (!reversalReason) return;
    try { await platformFinanceService.reverseRefund(id, reversalReason); await load(); }
    catch { setError('تعذر عكس الاسترداد'); }
  }

  const activePackages = student?.packages.filter(item => item.isActive && item.purchaseOperationId && item.paidAmount > 0) || [];

  return <div className="space-y-6" dir="rtl">
    <section className="admin-panel rounded-2xl p-6">
      <div className="mb-5">
        <h2 className="text-lg font-black">استرداد خارجي لطالب</h2>
        <p className="mt-1 text-xs text-[var(--admin-muted)]">يلغي الباقة بدون إضافة رصيد للطالب، ويسجل المبلغ الخارج من الخزنة كاسترداد في المركز المالي.</p>
      </div>
      <form onSubmit={createExternalRefund} className="grid gap-4 md:grid-cols-2">
        <div className="md:col-span-2">
          <label className="mb-1 block text-xs font-bold">رقم هاتف الطالب</label>
          <div className="flex gap-2">
            <input className="admin-input" value={phone} onChange={event => setPhone(event.target.value)} placeholder="01xxxxxxxxx" />
            <button className="admin-btn-ghost shrink-0" type="button" onClick={() => void searchStudent()}>بحث</button>
          </div>
          {students.length > 0 ? <div className="mt-2 divide-y rounded-xl border border-[var(--admin-border)]">{students.map(item => <button key={item.id} type="button" onClick={() => void selectStudent(item.id)} className="flex w-full justify-between p-3 text-right hover:bg-[var(--admin-card-strong)]"><b>{item.fullName}</b><bdi className="font-mono">{item.phoneNumber}</bdi></button>)}</div> : null}
          {student ? <p className="mt-2 rounded-xl bg-emerald-500/10 p-3 text-sm font-bold text-emerald-700">تم اختيار: {student.fullName} — <bdi>{student.phone}</bdi></p> : null}
        </div>
        <div>
          <label className="mb-1 block text-xs font-bold">الباقة التي سيتم إلغاؤها</label>
          <select className="admin-input" required value={grantId} onChange={event => { const value = event.target.value; setGrantId(value); setRefundAmount(String(activePackages.find(item => item.accessGrantId === value)?.paidAmount || '')); }} disabled={!student}>
            <option value="">اختر باقة نشطة</option>
            {activePackages.map(item => <option key={item.accessGrantId} value={item.accessGrantId}>{item.name} — المدفوع {money(item.paidAmount)}</option>)}
          </select>
        </div>
        <div>
          <label className="mb-1 block text-xs font-bold">الخزنة أو المحفظة التي خرج منها المبلغ</label>
          <select className="admin-input" required value={treasuryId} onChange={event => setTreasuryId(event.target.value)}>
            <option value="">اختر الخزنة</option>
            {bootstrap?.treasuryAccounts.map(item => <option key={item.id} value={item.id}>{item.name}{item.maskedIdentifier ? ` — ${item.maskedIdentifier}` : ''}</option>)}
          </select>
        </div>
        <div>
          <label className="mb-1 block text-xs font-bold">المبلغ المرتجع فعلياً</label>
          <input className="admin-input" type="number" min="0.01" max={activePackages.find(item => item.accessGrantId === grantId)?.paidAmount} step="0.01" required value={refundAmount} onChange={event => setRefundAmount(event.target.value)} placeholder="المبلغ بالجنيه" />
        </div>
        <div className="md:col-span-2">
          <label className="mb-1 block text-xs font-bold">سبب الاسترداد</label>
          <input className="admin-input" required value={reason} onChange={event => setReason(event.target.value)} placeholder="اكتب سبب إلغاء الباقة ورد المبلغ" />
        </div>
        <div className="md:col-span-2 flex justify-end">
          <button className="admin-btn-primary" type="submit" disabled={submitting || !student || !grantId || !treasuryId}>{submitting ? 'جارٍ التنفيذ…' : 'إلغاء الباقة وتسجيل الاسترداد'}</button>
        </div>
      </form>
    </section>

    <section className="admin-panel rounded-2xl p-6">
      <div className="mb-4 flex items-center justify-between"><div><h2 className="text-lg font-black">سجل الاستردادات</h2><p className="mt-1 text-xs text-[var(--admin-muted)]">يشمل الاستردادات المسجلة بالمركز المالي واستردادات الرصيد القديمة تلقائياً.</p></div><button className="admin-btn-ghost" type="button" onClick={() => void load()}>تحديث</button></div>
      {error ? <p role="alert" className="mb-3 text-rose-600">{error}</p> : null}
      <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-right"><th>الطالب</th><th>التاريخ</th><th>الطريقة</th><th>السبب</th><th>حصة المنصة</th><th>حصة المدرس</th><th>الإجمالي</th><th>الحالة</th><th /></tr></thead><tbody>{rows.map(row => <tr key={row.id} className="border-t border-[var(--admin-border)]"><td><Link href={`/admin/users/${row.studentId}`} className="font-bold text-[var(--admin-primary)] hover:underline">{row.studentName}</Link><bdi className="block font-mono text-xs text-[var(--admin-muted)]">{row.studentPhoneNumber}</bdi></td><td>{new Date(row.createdAt).toLocaleString('ar-EG', { dateStyle: 'medium', timeStyle: 'short' })}</td><td>{row.method === 2 ? 'كاش' : 'رصيد طالب'}</td><td>{row.reason || '—'}</td><td>{money(row.platformAmount)}</td><td>{money(row.teacherAmount)}</td><td className="font-bold">{money(row.totalAmount)}</td><td>{row.isHistorical ? 'استرداد قديم' : row.status === 3 ? 'معكوس' : row.status === 2 ? 'مقيد' : 'مسودة'}</td><td>{!row.isHistorical && row.status === 2 ? <button className="text-rose-600" type="button" onClick={() => void reverse(row.id)}>عكس</button> : null}</td></tr>)}</tbody></table>{rows.length === 0 ? <p className="py-8 text-center text-[var(--admin-muted)]">لا توجد استردادات.</p> : null}</div>
    </section>
  </div>;
}

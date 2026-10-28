'use client';

import { useState } from 'react';
import type { StudentProfileExtendedDto } from '@/services/admin-service';
import { getEducationStageLabel, getGradeLevelLabel, getStudyTrackLabel } from '@/lib/academic-labels';
import { formatCairoTimestamp } from '@/lib/cairo-time';

type StudentSectionProps = { student: StudentProfileExtendedDto };
const date = (timestamp?: string | null) => timestamp ? formatCairoTimestamp(timestamp) : 'غير مسجل';
const money = (amount: number) => new Intl.NumberFormat('ar-EG-u-nu-latn', { style: 'currency', currency: 'EGP' }).format(amount);
const purchaseLabels: Record<string, string> = { Code: 'كود', Gift: 'هدية', Balance: 'رصيد', Direct: 'إضافة مباشرة' };

export function TeacherStudentOverview({ student }: StudentSectionProps) {
  const fields: Array<[string, string | undefined]> = [
    ['الاسم', student.fullName], ['موبايل الطالب', student.phone], ['كود الطالب', student.studentCode],
    ['المرحلة', getEducationStageLabel(student.educationStage || '')], ['الصف', getGradeLevelLabel(student.grade || '')],
    ['الشعبة', student.studyTrack ? getStudyTrackLabel(student.studyTrack) : undefined],
    ['المدرسة', student.schoolName], ['المحافظة', student.governorate], ['المنطقة', student.district], ['العنوان', student.address],
    ['ولي الأمر', student.parentPhone], ['الهاتف البديل', student.secondaryPhone], ['هاتف الأم', student.motherPhone],
    ['هاتف ولي الأمر البديل', student.secondaryParentPhone], ['الجنسية', student.nationality],
    ['تاريخ الميلاد', student.dateOfBirth ? new Date(student.dateOfBirth).toLocaleDateString('ar-EG-u-nu-latn', { timeZone: 'Africa/Cairo' }) : undefined],
    ['تاريخ التسجيل', date(student.createdAt)],
  ];
  return <section aria-label="بيانات الطالب" className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 text-[var(--admin-text)]"><h3 className="mb-5 text-lg font-bold">البيانات الشخصية والدراسية</h3><dl className="grid gap-x-8 gap-y-5 sm:grid-cols-2 xl:grid-cols-3">{fields.map(([label, detail]) => <div key={label} className="min-w-0"><dt className="text-xs font-bold text-[var(--admin-muted)]">{label}</dt><dd className="mt-1 break-words text-sm font-bold">{detail || 'غير مسجل'}</dd></div>)}</dl></section>;
}

export function TeacherStudentSubscriptions({ student, showFinance }: StudentSectionProps & { showFinance: boolean }) {
  const [loadedAt] = useState(() => Date.now());
  return <section className="space-y-4 text-[var(--admin-text)]"><h3 className="text-lg font-bold">الاشتراكات وصلاحيات الوصول</h3>{student.packages.length === 0 && <p className="text-sm text-[var(--admin-muted)]">لا توجد اشتراكات مرتبطة بك.</p>}
    {student.packages.map(subscription => {
      const status = subscription.cancelledAt ? 'ملغي' : !subscription.isActive || (subscription.expiresAt && Date.parse(subscription.expiresAt) <= loadedAt) ? 'منتهي أو غير فعّال' : 'فعّال';
      return <article key={subscription.accessGrantId} className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4"><header className="flex flex-wrap items-center justify-between gap-2"><h4 className="font-bold">{subscription.name}</h4><span className="rounded-lg bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-bold">{status}</span></header><dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2"><div><dt className="text-[var(--admin-muted)]">تاريخ التفعيل</dt><dd>{date(subscription.enrolledAt)}</dd></div><div><dt className="text-[var(--admin-muted)]">الانتهاء</dt><dd>{subscription.expiresAt ? date(subscription.expiresAt) : 'بدون تاريخ انتهاء'}</dd></div><div><dt className="text-[var(--admin-muted)]">طريقة الحصول</dt><dd>{purchaseLabels[subscription.purchaseMethod] || 'غير مسجل'}</dd></div>{showFinance && <div><dt className="text-[var(--admin-muted)]">المبلغ المدفوع</dt><dd>{money(subscription.paidAmount)}</dd></div>}</dl>{subscription.cancellationReason && <p className="mt-4 text-sm">سبب الإلغاء: {subscription.cancellationReason}</p>}</article>;
    })}
  </section>;
}

export function TeacherStudentFinances({ student }: StudentSectionProps) {
  return <section className="space-y-5 text-[var(--admin-text)]"><h3 className="text-lg font-bold">الماليات المرتبطة بك</h3><p className="text-sm">رصيد الطالب المخصص لك: <strong>{money(student.promotionalBalances.reduce((total, balance) => total + balance.availableAmount, 0))}</strong></p>
    <h4 className="font-bold">حركات الرصيد المخصص</h4>{student.balanceTransactions.length === 0 ? <p className="text-sm text-[var(--admin-muted)]">لا توجد حركات مسجلة.</p> : <ul className="divide-y divide-[var(--admin-border)]">{student.balanceTransactions.map(transaction => <li key={transaction.id} className="flex flex-wrap items-center justify-between gap-3 py-4"><div><p className="text-sm font-bold">{transaction.contentName || transaction.description}</p><p className="mt-1 text-xs text-[var(--admin-muted)]">{date(transaction.createdAt)}</p></div><bdi className="text-sm font-bold">{money(transaction.amount)}</bdi></li>)}</ul>}
    <h4 className="font-bold">طلبات شحن رصيدك</h4>{student.rechargeRequests.length === 0 ? <p className="text-sm text-[var(--admin-muted)]">لا توجد طلبات شحن مرتبطة بك.</p> : <ul className="divide-y divide-[var(--admin-border)]">{student.rechargeRequests.map(request => <li key={request.id} className="flex flex-wrap justify-between gap-3 py-4 text-sm"><span>{date(request.createdAt)}</span><strong>{money(request.amount)}</strong><span>{({ Pending: 'قيد المراجعة', Approved: 'مقبول', Completed: 'مكتمل', Rejected: 'مرفوض', Cancelled: 'ملغي' } as Record<string, string>)[request.status] || request.status}</span></li>)}</ul>}
  </section>;
}

export function TeacherStudentNotes({ student }: StudentSectionProps) {
  return <section className="space-y-4 text-[var(--admin-text)]"><h3 className="text-lg font-bold">ملاحظاتك على الطالب</h3>{student.notes.length === 0 ? <p className="text-sm text-[var(--admin-muted)]">لا توجد ملاحظات مسجلة باسمك.</p> : student.notes.map(note => <article key={note.id} className="rounded-xl border border-[var(--admin-border)] p-4"><p className="whitespace-pre-wrap break-words text-sm">{note.content}</p><p className="mt-3 text-xs text-[var(--admin-muted)]">{note.adminName} · {date(note.createdAt)}{note.isPinned && ' · مثبتة'}</p></article>)}</section>;
}

export function TeacherStudentOverrides({ student }: StudentSectionProps) {
  return <section className="space-y-4 text-[var(--admin-text)]"><h3 className="text-lg font-bold">تجاوزات المشاهدة في محتواك</h3>{student.overrides.length === 0 ? <p className="text-sm text-[var(--admin-muted)]">لا توجد تجاوزات مسجلة.</p> : student.overrides.map(override => <article key={override.id} className="rounded-xl border border-[var(--admin-border)] p-4"><h4 className="font-bold">{override.videoTitle}</h4><p className="mt-2 text-sm">الحد السابق: {override.originalLimit} · الحد الجديد: {override.newLimit}</p><p className="mt-2 whitespace-pre-wrap text-sm">{override.reason}</p><p className="mt-3 text-xs text-[var(--admin-muted)]">{override.overrideBy} · {date(override.createdAt)}</p></article>)}</section>;
}

export function TeacherStudentAudit({ student }: StudentSectionProps) {
  return <section className="space-y-4 text-[var(--admin-text)]"><h3 className="text-lg font-bold">إجراءاتك المسجلة على الطالب</h3>{student.auditTrail.length === 0 ? <p className="text-sm text-[var(--admin-muted)]">لا توجد إجراءات مسجلة باسمك.</p> : <ul className="divide-y divide-[var(--admin-border)]">{student.auditTrail.map(activity => <li key={activity.id} className="py-4 text-sm"><p className="font-bold">{({ AddStudentNote: 'إضافة ملاحظة للطالب', DeleteStudentNote: 'حذف ملاحظة', OverrideVideoLimit: 'زيادة حد المشاهدة', UpdateStudentProfile: 'تحديث بيانات الطالب' } as Record<string, string>)[activity.action] || 'إجراء مسجل على الطالب'}</p><p className="mt-2 text-xs text-[var(--admin-muted)]">{activity.adminName} · {date(activity.date)}</p></li>)}</ul>}</section>;
}

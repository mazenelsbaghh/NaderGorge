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

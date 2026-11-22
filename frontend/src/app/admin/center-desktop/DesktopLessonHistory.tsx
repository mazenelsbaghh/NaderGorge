'use client';

import { useState } from 'react';
import { CheckCircle2, CircleHelp, XCircle } from 'lucide-react';
import type { DesktopStudentLesson } from '@/services/center-desktop-service';
import { desktopClock, desktopTimestamp } from './desktop-datetime';
import { attendanceLabel, homeworkLabel, paymentLines, examScore } from './student-report-format';

function Payment({ payment }: { payment: DesktopStudentLesson['payment'] }) {
  return <div className="space-y-1 text-sm">
    {paymentLines(payment).map((line, index) => <p key={index} className={index === 0 && payment.coverage === 'package' ? 'font-medium text-[var(--admin-success)]' : index === 0 && payment.coverage === 'pending' ? 'text-[var(--admin-danger)]' : undefined}>{line}</p>)}
  </div>;
}

export default function DesktopLessonHistory({ lessons }: { lessons: DesktopStudentLesson[] }) {
  const months = [...new Set(lessons.map(l => l.month))].sort((a, b) => b - a);
  const [month, setMonth] = useState('all');
  const [group, setGroup] = useState('');
  const selectedMonth = month;
  const groups = [...new Map(lessons.map(l => [l.groupId, l.group])).entries()];
  const filtered = lessons.filter(l => (selectedMonth === 'all' || String(l.month) === selectedMonth) && (!group || l.groupId === group))
    .sort((a, b) => b.month - a.month || (a.number ?? Infinity) - (b.number ?? Infinity) || a.group.localeCompare(b.group));
  const shownMonths = [...new Set(filtered.map(l => l.month))];
  return <section className="space-y-4" aria-label="سجل الحصص">
    <div className="flex flex-wrap items-end justify-between gap-3">
      <div><h3 className="text-lg font-bold">الحصص والحضور ودرجات الامتحانات</h3><p className="mt-1 text-sm text-[var(--admin-muted)]">الحصص التي بدأت فقط. «لم يُسجل» لا تعني غياب الطالب.</p></div>
      <div className="flex flex-wrap gap-3">
        <label className="space-y-1 text-sm"><span className="block">شهر الحصص</span><select className="admin-input" value={selectedMonth} onChange={e => setMonth(e.target.value)}><option value="all">كل الشهور</option>{months.map(m => <option key={m} value={String(m)}>{lessons.find(l => l.month === m)?.monthName || `الشهر ${m}`}</option>)}</select></label>
        <label className="space-y-1 text-sm"><span className="block">مجموعة الحصص</span><select className="admin-input" value={group} onChange={e => setGroup(e.target.value)}><option value="">كل المجموعات</option>{groups.map(([id, name]) => <option key={id} value={id}>{name || 'غير متوفرة'}</option>)}</select></label>
      </div>
    </div>
    {!filtered.length && <p className="py-6 text-center text-[var(--admin-muted)]">لا توجد حصص مسجلة لهذه الاختيارات.</p>}
    {shownMonths.map(m => <div key={m} className="space-y-2">
      <h4 className="font-semibold">{lessons.find(l => l.month === m)?.monthName || `الشهر ${m}`}</h4>
      <div className="overflow-x-auto rounded-lg border border-[var(--admin-border)]"><table className="w-full min-w-[850px] text-start text-sm">
        <caption className="sr-only">سجل حصص {lessons.find(l => l.month === m)?.monthName || `الشهر ${m}`}</caption>
        <thead className="bg-[var(--admin-card-soft)]"><tr>{['الحصة والمجموعة', 'موعد الحصة', 'الحضور ووقت التسجيل', 'الدفع', 'درجة الامتحان', 'الواجب'].map(label => <th key={label} scope="col" className="px-4 py-3 text-start font-semibold">{label}</th>)}</tr></thead>
        <tbody className="divide-y divide-[var(--admin-border)]">{filtered.filter(l => l.month === m).map(lesson => {
          const status = lesson.attendance?.status;
          const Icon = status === 'absent' ? XCircle : status === 'present' || status === 'makeup' ? CheckCircle2 : CircleHelp;
          return <tr key={lesson.id} className="align-top hover:bg-[var(--admin-card-soft)]">
            <th scope="row" className="space-y-1 px-4 py-4 text-start"><p className="font-semibold">الحصة {lesson.number ?? 'غير محددة'}</p>{lesson.name && <p className="font-normal">{lesson.name}</p>}<p className="font-normal text-[var(--admin-muted)]">{lesson.group || 'مجموعة غير متوفرة'}</p>{lesson.center && <p className="font-normal text-[var(--admin-muted)]">{lesson.center}</p>}</th>
            <td className="px-4 py-4">{lesson.date ? <time dateTime={lesson.date}>{desktopTimestamp(lesson.date)}</time> : 'التاريخ غير متوفر'}</td>
            <td className="space-y-1 px-4 py-4"><p className={`inline-flex items-center gap-1.5 font-medium ${status === 'absent' ? 'text-[var(--admin-danger)]' : status ? 'text-[var(--admin-success)]' : 'text-[var(--admin-muted)]'}`}><Icon aria-hidden className="h-4 w-4 shrink-0" />{attendanceLabel(status)}</p>
              {lesson.attendance && <p className="text-xs text-[var(--admin-muted)]">{lesson.attendance.recordedAt && Number.isFinite(Date.parse(lesson.attendance.recordedAt)) ? <>{status === 'absent' ? 'تسجيل الغياب' : 'تسجيل الحضور'}: <time dateTime={lesson.attendance.recordedAt} title={desktopTimestamp(lesson.attendance.recordedAt)}>{desktopClock(lesson.attendance.recordedAt)}</time> · القاهرة</> : 'وقت التسجيل غير متوفر'}</p>}
              {lesson.attendance?.makeupSourceGroup && <p className="text-xs">تعويض عن {lesson.attendance.makeupSourceGroup}</p>}
            </td>
            <td className="px-4 py-4"><Payment payment={lesson.payment} /></td>
            <td className="space-y-2 px-4 py-4">{lesson.exams.length ? lesson.exams.map(exam => <div key={exam.id}>{exam.name && <p className="text-xs text-[var(--admin-muted)]">{exam.name}</p>}<p className={exam.absent ? 'text-[var(--admin-danger)]' : 'font-semibold'}>{examScore(exam)}</p></div>) : <span className="text-[var(--admin-muted)]">لا يوجد امتحان مسجل</span>}</td>
            <td className="px-4 py-4">{homeworkLabel(lesson.homework)}</td>
          </tr>;
        })}</tbody>
      </table></div>
    </div>)}
  </section>;
}

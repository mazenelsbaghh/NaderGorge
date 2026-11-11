'use client';

import type { FollowUpRow } from '@/services/student-follow-up-service';
import { getEducationStageLabel, getGradeLevelLabel, getStudyTrackLabel } from '@/lib/academic-labels';

export function StudentReportDetails({ student }: { student: FollowUpRow }) {
  const profile = student.details;
  const fields = [
    ['كود الطالب', profile?.studentCode], ['المرحلة', getEducationStageLabel(student.stage)], ['الصف', getGradeLevelLabel(student.grade)],
    ['الشعبة', getStudyTrackLabel(profile?.studyTrack)], ['المحافظة', profile?.governorate], ['المنطقة', profile?.district],
    ['العنوان', profile?.address], ['المدرسة', profile?.schoolName], ['موبايل إضافي', profile?.secondaryPhone],
    ['ولي الأمر', student.parentPhone], ['ولي الأمر الإضافي', profile?.secondaryParentPhone],
    ['الأم', profile?.motherPhone], ['الجنسية', profile?.nationality],
  ];
  return <details className="mt-2 min-w-48 text-xs"><summary className="min-h-11 cursor-pointer py-3 font-bold text-[var(--admin-primary)]">بيانات الطالب كاملة</summary>
    <dl className="space-y-2 pb-2">{fields.map(([label, content]) => <div key={label} className="flex flex-wrap justify-between gap-2"><dt className="text-[var(--admin-muted)]">{label}</dt><dd className="max-w-64 whitespace-normal break-words"><bdi>{content?.trim() || 'غير مسجل'}</bdi></dd></div>)}</dl>
  </details>;
}

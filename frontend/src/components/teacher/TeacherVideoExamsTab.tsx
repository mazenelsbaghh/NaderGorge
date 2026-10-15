"use client";

import { useState } from "react";
import Link from "next/link";
import { BookCheck, Plus } from "lucide-react";
import { UnifiedAssessmentBuilder } from "@/components/admin/UnifiedAssessmentBuilder";
import type { LessonCockpitVideoDto } from "@/services/admin-service";

interface TeacherVideoExamsTabProps {
  lessonId: string;
  videos: LessonCockpitVideoDto[];
  onRefresh: () => void;
}

export function TeacherVideoExamsTab({ lessonId, videos, onRefresh }: TeacherVideoExamsTabProps) {
  const [creatingForVideoId, setCreatingForVideoId] = useState<string | null>(null);

  if (videos.length === 0) {
    return <p className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 text-[var(--admin-muted)]">
      لا توجد بارتات في هذه الحصة بعد. بعد إضافة الإدارة للفيديوهات، يمكنك إنشاء امتحان لكل بارت هنا.
    </p>;
  }

  return <div className="space-y-6">
    <p className="text-sm text-[var(--admin-muted)]">
      اختر البارت وأنشئ امتحانه، ثم افتح الامتحان لإضافة الأسئلة. امتحانات البارتات مستقلة عن امتحان الحصة.
    </p>
    {videos.map(video => <VideoExamCard key={video.id} video={video}
      creating={creatingForVideoId === video.id}
      onToggle={() => setCreatingForVideoId(current => current === video.id ? null : video.id)}>
      <UnifiedAssessmentBuilder key={video.id} type="exam" lessonId={lessonId}
        videos={[video]} forceTargetType="Video" surface="teacher" onSuccess={onRefresh} />
    </VideoExamCard>)}
  </div>;
}

function VideoExamCard({ video, creating, onToggle, children }: {
  video: LessonCockpitVideoDto;
  creating: boolean;
  onToggle: () => void;
  children: React.ReactNode;
}) {
  const exams = video.exams?.length ? video.exams
    : video.examId ? [{ examId: video.examId, title: "الامتحان المرفق بالبارت" }] : [];

  return <section aria-label={`امتحانات ${video.title}`}
    className="space-y-4 rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h3 className="min-w-0 break-words text-lg font-bold text-[var(--admin-text)]">{video.title}</h3>
      <button type="button" onClick={onToggle} aria-expanded={creating}
        aria-controls={`video-exam-builder-${video.id}`} className="admin-btn-primary inline-flex min-h-11 items-center gap-2">
        <Plus className="h-4 w-4" aria-hidden="true" />
        {creating ? "إغلاق نموذج الامتحان" : "إنشاء امتحان للبارت"}
      </button>
    </div>
    {exams.length > 0 ? <ul className="space-y-2">
      {exams.map(exam => <li key={exam.examId}>
        <Link href={`/teacher/packages/exams/${exam.examId}`}
          className="inline-flex min-h-11 items-center gap-2 text-sm font-bold text-[var(--admin-primary)] hover:underline">
          <BookCheck className="h-4 w-4 shrink-0" aria-hidden="true" />
          <span className="break-words">{exam.title} — إدارة الأسئلة والامتحان</span>
        </Link>
      </li>)}
    </ul> : <p className="text-sm text-[var(--admin-muted)]">لم يُضف امتحان لهذا البارت بعد.</p>}
    {creating && <div id={`video-exam-builder-${video.id}`} className="border-t border-[var(--admin-border)] pt-4">
      {children}
    </div>}
  </section>;
}

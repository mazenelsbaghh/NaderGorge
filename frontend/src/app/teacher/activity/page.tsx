"use client";

import { useState, useEffect } from "react";
import { Users, AlertTriangle, Play, Flame, Clock, Calendar } from "lucide-react";
import { teacherService, TeacherActivityDto } from "@/services/teacher-service";

import { TeacherShellChrome } from "@/components/teacher/TeacherShellChrome";

export default function TeacherActivityPage() {
  const [data, setData] = useState<TeacherActivityDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    teacherService.getTeacherActivity()
      .then((res) => {
        if (res.success) {
          setData(res.data);
        }
      })
      .catch((err) => console.error("Error fetching activity:", err))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <TeacherShellChrome
        activePath="/teacher/activity"
        sectionLabel="نشاط الطلاب"
        pageTitle="تتبع نشاط الطلاب"
        subtitle="راقب تفاعل طلابك مع المحتوى التعليمي، واكتشف الفيديوهات الأكثر مشاهدة ومتابعة حالات الخمول."
      >
        <div className="flex h-[60vh] items-center justify-center" dir="rtl">
          <div className="text-center space-y-4">
            <div className="h-12 w-12 border-4 border-[var(--admin-primary)] border-t-transparent rounded-full animate-spin mx-auto"></div>
            <p className="text-sm text-[var(--admin-muted)]">جاري تحميل نشاط الطلاب وإحصائيات المشاهدة...</p>
          </div>
        </div>
      </TeacherShellChrome>
    );
  }

  return (
    <TeacherShellChrome
      activePath="/teacher/activity"
      sectionLabel="نشاط الطلاب"
      pageTitle="تتبع نشاط الطلاب"
      subtitle="راقب تفاعل طلابك مع المحتوى التعليمي، واكتشف الفيديوهات الأكثر مشاهدة ومتابعة حالات الخمول."
    >
      <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
        <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
          {/* Left column: Alerts for inactive students */}
          <div className="lg:col-span-1 space-y-6">
            <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-xl relative overflow-hidden">
              <div className="flex items-center gap-3 border-b border-[var(--admin-border)] pb-4 mb-4">
                <div className="bg-rose-500/10 p-2.5 rounded-xl text-rose-500">
                  <AlertTriangle className="h-5 w-5" />
                </div>
                <div>
                  <h2 className="text-lg font-black text-[var(--admin-text)]">تنبيهات خمول الطلاب</h2>
                  <p className="text-xs text-[var(--admin-muted)]">الطلاب المنقطعون عن المشاهدة لأكثر من 7 أيام</p>
                </div>
              </div>

              <div className="space-y-4 max-h-[600px] overflow-y-auto custom-scrollbar">
                {!data?.inactiveStudentAlerts || data.inactiveStudentAlerts.length === 0 ? (
                  <div className="text-center py-8 text-sm text-[var(--admin-muted)]">
                    لا توجد تنبيهات خمول حالياً. جميع الطلاب متفاعلون!
                  </div>
                ) : (
                  data.inactiveStudentAlerts.map((alert) => (
                    <div
                      key={alert.studentId}
                      className="p-4 rounded-2xl bg-rose-500/5 border border-rose-500/10 flex items-center justify-between gap-4 transition hover:bg-rose-500/10"
                    >
                      <div>
                        <h4 className="font-bold text-[var(--admin-text)] text-sm">{alert.studentName}</h4>
                        <p className="text-xs text-[var(--admin-muted)] mt-1">
                          الباقة: <span className="font-semibold">{alert.packageName}</span>
                        </p>
                        <p className="text-xs text-rose-500 mt-2 flex items-center gap-1 font-semibold">
                          <Clock className="h-3 w-3" />
                          منذ {alert.daysInactive} يوم
                        </p>
                      </div>
                      <div className="text-rose-500 bg-rose-500/10 px-3 py-1 rounded-full text-xs font-black">
                        خامل
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>

          {/* Center & Right columns: Active Students & Most Watched Videos */}
          <div className="lg:col-span-2 space-y-8">
            {/* Active Students List */}
            <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-xl">
              <div className="flex items-center gap-3 border-b border-[var(--admin-border)] pb-4 mb-4">
                <div className="bg-[var(--admin-primary-15)] p-2.5 rounded-xl text-[var(--admin-primary)]">
                  <Users className="h-5 w-5" />
                </div>
                <div>
                  <h2 className="text-lg font-black text-[var(--admin-text)] font-tajawal">النشاط الأخير للطلاب</h2>
                  <p className="text-xs text-[var(--admin-muted)]">آخر مشاهدات تمت لدروس وباقات المعلم</p>
                </div>
              </div>

              <div className="space-y-4">
                {!data?.activeStudents || data.activeStudents.length === 0 ? (
                  <div className="text-center py-12 text-sm text-[var(--admin-muted)]">
                    لا توجد أنشطة مسجلة حالياً لطلابك.
                  </div>
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {data.activeStudents.map((student, idx) => (
                      <div
                        key={student.studentId + "-" + idx}
                        className="p-4 rounded-2xl bg-[var(--admin-bg)] border border-[var(--admin-border)] flex flex-col justify-between gap-3 hover:border-[var(--admin-primary)] transition"
                      >
                        <div className="flex items-start justify-between">
                          <div>
                            <h4 className="font-bold text-[var(--admin-text)] text-sm">{student.studentName}</h4>
                            <span className="text-[10px] bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-black px-2 py-0.5 rounded-full mt-1.5 inline-block">
                              {student.packageName}
                            </span>
                          </div>
                          <span className="text-[10px] text-[var(--admin-muted)] flex items-center gap-1">
                            <Calendar className="h-3 w-3" />
                            {student.lastActivityAt ? new Date(student.lastActivityAt).toLocaleDateString("ar-EG", {
                              hour: "2-digit",
                              minute: "2-digit",
                            }) : "غير معروف"}
                          </span>
                        </div>
                        <div className="flex items-center gap-2 text-xs text-[var(--admin-text)] bg-[var(--admin-card)]/55 p-2.5 rounded-xl border border-[var(--admin-border)]">
                          <Play className="h-3.5 w-3.5 text-[var(--admin-primary)] shrink-0" />
                          <span className="truncate font-medium">{student.lastWatchedVideoTitle}</span>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* Most Watched Videos */}
            <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-xl">
              <div className="flex items-center gap-3 border-b border-[var(--admin-border)] pb-4 mb-4">
                <div className="bg-amber-500/10 p-2.5 rounded-xl text-amber-500">
                  <Flame className="h-5 w-5" />
                </div>
                <div>
                  <h2 className="text-lg font-black text-[var(--admin-text)]">الفيديوهات الأكثر مشاهدة</h2>
                  <p className="text-xs text-[var(--admin-muted)] font-medium">الدروس ومقاطع الفيديو الأكثر تفاعلاً من قبل الطلاب</p>
                </div>
              </div>

              <div className="overflow-x-auto">
                {!data?.mostWatchedVideos || data.mostWatchedVideos.length === 0 ? (
                  <div className="text-center py-12 text-sm text-[var(--admin-muted)]">
                    لم يتم رصد مشاهدات للفيديوهات بعد.
                  </div>
                ) : (
                  <table className="w-full text-right border-collapse">
                    <thead>
                      <tr className="border-b border-[var(--admin-border)] text-xs text-[var(--admin-muted)] font-black">
                        <th className="pb-3 pt-1">الفيديو / الدرس</th>
                        <th className="pb-3 pt-1 text-center">عدد المشاهدات</th>
                        <th className="pb-3 pt-1 text-left">إجمالي وقت المشاهدة</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[var(--admin-border)]">
                      {data.mostWatchedVideos.map((video) => (
                        <tr key={video.videoId} className="hover:bg-[var(--admin-bg)]/30 transition text-sm">
                          <td className="py-4">
                            <div className="font-bold text-[var(--admin-text)]">{video.videoTitle}</div>
                            <div className="text-xs text-[var(--admin-muted)] mt-0.5">الدرس: {video.lessonTitle}</div>
                          </td>
                          <td className="py-4 text-center font-black text-[var(--admin-primary)]">
                            {video.totalWatchCount}
                          </td>
                          <td className="py-4 text-left font-medium text-[var(--admin-muted)]">
                            {Math.round(video.totalTimeWatchedSeconds / 60)} دقيقة
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </TeacherShellChrome>
  );
}

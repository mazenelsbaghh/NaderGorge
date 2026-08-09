"use client";

import { useCallback, useState, useEffect } from "react";
import { Users, AlertTriangle, Play, Flame, Clock, Calendar, Activity, Timer, TrendingUp, CheckCircle2 } from "lucide-react";
import {
  teacherService,
  TeacherActivityDto,
  TeacherActiveStudentDto,
  TeacherInactiveStudentAlertDto,
  TeacherMostWatchedVideoDto,
} from "@/services/teacher-service";
import { AdminColumn, AdminDataTable, AdminStatCard } from "@/components/admin";

import { TeacherPage } from "@/components/teacher/TeacherShellChrome";

const formatDateTime = (value: string | null) => {
  if (!value) return "غير معروف";
  return new Intl.DateTimeFormat("ar-EG", { timeZone: 'Africa/Cairo',
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
};

const formatMinutes = (seconds: number) => {
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes.toLocaleString("ar-EG")} دقيقة`;
  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;
  return remainingMinutes === 0
    ? `${hours.toLocaleString("ar-EG")} ساعة`
    : `${hours.toLocaleString("ar-EG")} ساعة و${remainingMinutes.toLocaleString("ar-EG")} دقيقة`;
};

export default function TeacherActivityPageClient() {
  const [data, setData] = useState<TeacherActivityDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadActivity = useCallback(() => {
    setLoading(true);
    setError("");
    teacherService.getTeacherActivity()
      .then((res) => {
        if (res.success) {
          setData(res.data);
          return;
        }
        setError(res.message || "تعذر تحميل نشاط الطلاب.");
      })
      .catch(() => setError("تعذر تحميل نشاط الطلاب. حاول مرة أخرى."))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    loadActivity();
  }, [loadActivity]);

  const activeStudents = data?.activeStudents ?? [];
  const mostWatchedVideos = data?.mostWatchedVideos ?? [];
  const inactiveStudentAlerts = data?.inactiveStudentAlerts ?? [];
  const totalWatchCount = mostWatchedVideos.reduce((sum, video) => sum + video.totalWatchCount, 0);
  const totalWatchSeconds = mostWatchedVideos.reduce((sum, video) => sum + video.totalTimeWatchedSeconds, 0);
  const latestActivity = activeStudents
    .map((student) => student.lastActivityAt)
    .filter(Boolean)
    .sort((a, b) => new Date(b as string).getTime() - new Date(a as string).getTime())[0] ?? null;
  const activeStudentColumns: AdminColumn<TeacherActiveStudentDto>[] = [
    {
      key: "student",
      label: "الطالب",
      render: (student) => (
        <div>
          <p className="font-black text-[var(--admin-text)]">{student.studentName}</p>
          <p className="mt-1 text-xs font-bold text-[var(--admin-primary)]">{student.packageName}</p>
        </div>
      ),
    },
    {
      key: "lastVideo",
      label: "آخر فيديو",
      render: (student) => (
        <span className="line-clamp-1 max-w-[360px] font-bold text-[var(--admin-text)]">
          {student.lastWatchedVideoTitle || "غير متوفر"}
        </span>
      ),
    },
    {
      key: "lastActivity",
      label: "وقت آخر نشاط",
      render: (student) => (
        <span className="font-bold text-[var(--admin-muted)]">
          {formatDateTime(student.lastActivityAt)}
        </span>
      ),
    },
    {
      key: "status",
      label: "الحالة",
      align: "center",
      render: () => (
        <span className="inline-flex rounded-full bg-emerald-500/10 px-3 py-1 text-xs font-black text-emerald-600">
          نشط
        </span>
      ),
    },
  ];
  const videoColumns: AdminColumn<TeacherMostWatchedVideoDto>[] = [
    {
      key: "video",
      label: "الفيديو",
      render: (video) => (
        <div>
          <p className="line-clamp-1 max-w-[360px] font-black text-[var(--admin-text)]">{video.videoTitle}</p>
          <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">الدرس: {video.lessonTitle}</p>
        </div>
      ),
    },
    {
      key: "watchCount",
      label: "المشاهدات",
      align: "center",
      render: (video) => (
        <span className="font-black text-[var(--admin-primary)]">
          {video.totalWatchCount.toLocaleString("ar-EG")}
        </span>
      ),
    },
    {
      key: "watchTime",
      label: "الوقت المحتسب",
      align: "left",
      render: (video) => (
        <span className="font-bold text-[var(--admin-text)]">
          {formatMinutes(video.totalTimeWatchedSeconds)}
        </span>
      ),
    },
    {
      key: "playbackRate",
      label: "متوسط السرعة",
      align: "center",
      render: (video) => (
        <span className="font-bold text-[var(--admin-text)]" dir="ltr">
          {video.averagePlaybackRate.toFixed(2).replace(/\\.00$/, "")}×
        </span>
      ),
    },
  ];
  const inactiveColumns: AdminColumn<TeacherInactiveStudentAlertDto>[] = [
    {
      key: "student",
      label: "الطالب",
      render: (alert) => (
        <div>
          <p className="font-black text-[var(--admin-text)]">{alert.studentName}</p>
          <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">{alert.packageName}</p>
        </div>
      ),
    },
    {
      key: "daysInactive",
      label: "أيام الخمول",
      align: "center",
      render: (alert) => (
        <span className="inline-flex rounded-full bg-rose-500/10 px-3 py-1 text-xs font-black text-rose-600">
          {alert.daysInactive.toLocaleString("ar-EG")} يوم
        </span>
      ),
    },
    {
      key: "lastActivity",
      label: "آخر نشاط",
      render: (alert) => (
        <span className="font-bold text-rose-600">{formatDateTime(alert.lastActivityAt)}</span>
      ),
    },
  ];

  if (loading) {
    return (
      <TeacherPage
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
      </TeacherPage>
    );
  }

  return (
    <TeacherPage
      activePath="/teacher/activity"
      sectionLabel="نشاط الطلاب"
      pageTitle="تتبع نشاط الطلاب"
      subtitle="راقب تفاعل طلابك مع المحتوى التعليمي، واكتشف الفيديوهات الأكثر مشاهدة ومتابعة حالات الخمول."
    >
      <div className="space-y-8" dir="rtl">
        {error ? (
          <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 text-sm font-bold text-[var(--admin-danger)]">
            <span>{error}</span>
            <button
              type="button"
              onClick={loadActivity}
              className="min-h-11 rounded-lg px-4 underline underline-offset-4"
            >
              إعادة المحاولة
            </button>
          </div>
        ) : null}

        <section className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
          <AdminStatCard
            variant="light"
            icon={Users}
            label="طلاب ظهر لهم نشاط"
            value={activeStudents.length.toLocaleString("ar-EG")}
            subtitle="حسب آخر مشاهدات مسجلة"
          />
          <AdminStatCard
            variant="muted"
            icon={Play}
            label="إجمالي المشاهدات"
            value={totalWatchCount.toLocaleString("ar-EG")}
            subtitle="ضمن الفيديوهات الأكثر مشاهدة"
          />
          <AdminStatCard
            variant="accent"
            icon={Timer}
            label="وقت مشاهدة مرصود"
            value={formatMinutes(totalWatchSeconds)}
            subtitle="تقريب لأقرب دقيقة"
          />
          <AdminStatCard
            variant={inactiveStudentAlerts.length > 0 ? "muted" : "light"}
            icon={AlertTriangle}
            label="تنبيهات خمول"
            value={inactiveStudentAlerts.length.toLocaleString("ar-EG")}
            subtitle={latestActivity ? `آخر نشاط: ${formatDateTime(latestActivity)}` : "لا توجد بيانات حديثة"}
          />
        </section>

        <div className="grid grid-cols-1 gap-8 xl:grid-cols-[1.4fr_0.9fr]">
          <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm sm:p-6">
            <div className="mb-5 flex flex-wrap items-start justify-between gap-4 border-b border-[var(--admin-border)] pb-4">
              <div className="flex items-center gap-3">
                <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                  <Activity className="h-5 w-5" />
                </div>
                <div>
                  <h2 className="text-lg font-black text-[var(--admin-text)]">كروت نشاط الطلاب</h2>
                  <p className="text-xs font-medium text-[var(--admin-muted)]">اسم الطالب، الباقة، آخر فيديو، ووقت النشاط بالتحديد</p>
                </div>
              </div>
              <span className="rounded-full bg-[var(--admin-card-strong)] px-3 py-1 text-xs font-black text-[var(--admin-text)]">
                {activeStudents.length.toLocaleString("ar-EG")} نشاط
              </span>
            </div>

            {activeStudents.length === 0 ? (
              <div className="flex min-h-56 flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-bg)] p-8 text-center">
                <CheckCircle2 className="mb-3 h-8 w-8 text-[var(--admin-primary)]" />
                <p className="text-sm font-bold text-[var(--admin-text)]">لا توجد أنشطة مسجلة حالياً.</p>
                <p className="mt-1 text-xs font-medium text-[var(--admin-muted)]">ستظهر هنا آخر مشاهدة لكل طالب عند توفر بيانات جديدة.</p>
              </div>
            ) : (
              <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
                {activeStudents.map((student, idx) => (
                  <article
                    key={student.studentId + "-" + idx}
                    className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4 transition hover:border-[var(--admin-primary)] hover:bg-[var(--admin-card)]"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <h3 className="truncate text-base font-black text-[var(--admin-text)]">{student.studentName}</h3>
                        <p className="mt-1 line-clamp-1 text-xs font-bold text-[var(--admin-primary)]">{student.packageName}</p>
                      </div>
                      <span className="shrink-0 rounded-full bg-emerald-500/10 px-3 py-1 text-xs font-black text-emerald-600">
                        نشط
                      </span>
                    </div>
                    <div className="mt-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3">
                      <div className="mb-2 flex items-center gap-2 text-xs font-black text-[var(--admin-text)]">
                        <Play className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                        آخر فيديو
                      </div>
                      <p className="line-clamp-2 text-sm font-bold text-[var(--admin-text)]">{student.lastWatchedVideoTitle || "غير متوفر"}</p>
                    </div>
                    <div className="mt-3 flex items-center gap-2 text-xs font-bold text-[var(--admin-muted)]">
                      <Calendar className="h-3.5 w-3.5" />
                      <span>{formatDateTime(student.lastActivityAt)}</span>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </section>

          <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm sm:p-6">
            <div className="mb-5 flex items-center gap-3 border-b border-[var(--admin-border)] pb-4">
              <div className="rounded-2xl bg-rose-500/10 p-2.5 text-rose-600">
                <AlertTriangle className="h-5 w-5" />
              </div>
              <div>
                <h2 className="text-lg font-black text-[var(--admin-text)]">طلاب يحتاجون متابعة</h2>
                <p className="text-xs font-medium text-[var(--admin-muted)]">خمول أكثر من 7 أيام مع آخر باقة مرتبطة</p>
              </div>
            </div>

            {inactiveStudentAlerts.length === 0 ? (
              <div className="flex min-h-48 flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-bg)] p-6 text-center">
                <CheckCircle2 className="mb-3 h-8 w-8 text-emerald-600" />
                <p className="text-sm font-bold text-[var(--admin-text)]">لا توجد تنبيهات خمول حالياً.</p>
              </div>
            ) : (
              <div className="space-y-3">
                {inactiveStudentAlerts.map((alert) => (
                  <article key={alert.studentId} className="rounded-2xl border border-rose-500/15 bg-rose-500/5 p-4">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <h3 className="truncate text-sm font-black text-[var(--admin-text)]">{alert.studentName}</h3>
                        <p className="mt-1 line-clamp-1 text-xs font-bold text-[var(--admin-muted)]">{alert.packageName}</p>
                      </div>
                      <span className="rounded-full bg-rose-500/10 px-3 py-1 text-xs font-black text-rose-600">
                        {alert.daysInactive.toLocaleString("ar-EG")} يوم
                      </span>
                    </div>
                    <p className="mt-3 flex items-center gap-2 text-xs font-bold text-rose-600">
                      <Clock className="h-3.5 w-3.5" />
                      آخر نشاط: {formatDateTime(alert.lastActivityAt)}
                    </p>
                  </article>
                ))}
              </div>
            )}
          </section>
        </div>

        <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm sm:p-6">
          <div className="mb-5 flex flex-wrap items-start justify-between gap-4 border-b border-[var(--admin-border)] pb-4">
            <div className="flex items-center gap-3">
              <div className="rounded-2xl bg-amber-500/10 p-2.5 text-amber-600">
                <Flame className="h-5 w-5" />
              </div>
              <div>
                <h2 className="text-lg font-black text-[var(--admin-text)]">الفيديوهات الأقوى تفاعلاً</h2>
                <p className="text-xs font-medium text-[var(--admin-muted)]">مرتبة حسب المشاهدات مع وقت المشاهدة لكل فيديو</p>
              </div>
            </div>
            <TrendingUp className="h-5 w-5 text-[var(--admin-primary)]" />
          </div>

          {mostWatchedVideos.length === 0 ? (
            <div className="flex min-h-48 flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-bg)] p-8 text-center">
              <Play className="mb-3 h-8 w-8 text-[var(--admin-primary)]" />
              <p className="text-sm font-bold text-[var(--admin-text)]">لم يتم رصد مشاهدات للفيديوهات بعد.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-3">
              {mostWatchedVideos.map((video, index) => (
                <article key={video.videoId} className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4">
                  <div className="mb-4 flex items-center justify-between gap-3">
                    <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-sm font-black text-[var(--admin-primary)]">
                      {(index + 1).toLocaleString("ar-EG")}
                    </span>
                    <span className="rounded-full bg-amber-500/10 px-3 py-1 text-xs font-black text-amber-700">
                      {video.totalWatchCount.toLocaleString("ar-EG")} مشاهدة
                    </span>
                  </div>
                  <h3 className="line-clamp-2 min-h-11 text-sm font-black leading-6 text-[var(--admin-text)]">{video.videoTitle}</h3>
                  <p className="mt-2 line-clamp-1 text-xs font-bold text-[var(--admin-muted)]">الدرس: {video.lessonTitle}</p>
                  <div className="mt-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-xs font-black text-[var(--admin-text)]">
                    إجمالي الوقت: {formatMinutes(video.totalTimeWatchedSeconds)}
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm sm:p-6">
          <div className="mb-5 flex items-center gap-3 border-b border-[var(--admin-border)] pb-4">
            <div className="rounded-2xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
              <Activity className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-lg font-black text-[var(--admin-text)]">الجداول التفصيلية</h2>
              <p className="text-xs font-medium text-[var(--admin-muted)]">نفس البيانات بشكل جدولي للمراجعة السريعة والمقارنة</p>
            </div>
          </div>

          <div className="space-y-8">
            <div>
              <h3 className="mb-3 text-sm font-black text-[var(--admin-text)]">آخر نشاط لكل طالب</h3>
              <AdminDataTable
                data={activeStudents}
                columns={activeStudentColumns}
                rowKey={(student) => student.studentId}
                emptyMessage="لا توجد أنشطة مسجلة."
                pageSize={6}
              />
            </div>

            <div>
              <h3 className="mb-3 text-sm font-black text-[var(--admin-text)]">تفاصيل الفيديوهات الأكثر مشاهدة</h3>
              <AdminDataTable
                data={mostWatchedVideos}
                columns={videoColumns}
                rowKey={(video) => video.videoId}
                emptyMessage="لا توجد مشاهدات مسجلة."
                pageSize={6}
              />
            </div>

            <div>
              <h3 className="mb-3 text-sm font-black text-[var(--admin-text)]">تنبيهات الخمول</h3>
              <AdminDataTable
                data={inactiveStudentAlerts}
                columns={inactiveColumns}
                rowKey={(alert) => alert.studentId}
                emptyMessage="لا توجد تنبيهات خمول."
                pageSize={6}
              />
            </div>
          </div>
        </section>
      </div>
    </TeacherPage>
  );
}

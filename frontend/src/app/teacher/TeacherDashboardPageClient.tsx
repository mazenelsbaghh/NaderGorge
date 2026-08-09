"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { ArrowLeft, BookOpen, KeyRound, Shield, Users, GraduationCap } from "lucide-react";
import { useAuthStore } from "@/stores/auth-store";
import { AdminStatCard } from "@/components/admin";
import { teacherService, TeacherDashboardStatsDto } from "@/services/teacher-service";

import { TeacherPage } from "@/components/teacher/TeacherShellChrome";

export default function TeacherDashboardPageClient() {
  const { user } = useAuthStore();
  const [stats, setStats] = useState<TeacherDashboardStatsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadDashboardStats = useCallback(() => {
    setLoading(true);
    setError("");
    teacherService.getDashboardStats()
      .then((res) => {
        if (res.success) {
          setStats(res.data);
          return;
        }
        setError(res.message || "تعذر تحميل ملخص لوحة المعلم.");
      })
      .catch(() => setError("تعذر تحميل ملخص لوحة المعلم. حاول مرة أخرى."))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    loadDashboardStats();
  }, [loadDashboardStats]);

  const quickLinks = [
    {
      href: "/teacher/essays",
      title: "الإجابات المعلقة",
      body: "راجع الإجابات المقالية التي تنتظر التصحيح.",
      icon: GraduationCap,
      count: stats?.pendingEssaysCount ?? 0,
    },
    {
      href: "/teacher/packages",
      title: "إدارة المحتوى",
      body: "نظّم الباقات والدروس والمواد التعليمية.",
      icon: BookOpen,
    },
    {
      href: "/teacher/codes",
      title: "أكواد الوصول",
      body: "أنشئ الأكواد وتابع استخدامها.",
      icon: KeyRound,
    },
    {
      href: "/teacher/public-exams",
      title: "الامتحانات العامة",
      body: "أنشئ امتحاناً يظهر خارج الحصص ويرتبط بك تلقائياً.",
      icon: Shield,
    },
  ];

  return (
    <TeacherPage
      activePath="/teacher"
      sectionLabel="لوحة التحكم"
      pageTitle="لوحة المعلم"
      subtitle="مرحباً بك في مساحتك التعليمية الخاصة."
    >
      <div className="space-y-8" dir="rtl">
        <section className="rounded-2xl bg-[var(--admin-card-soft)] p-6">
          <p className="text-sm font-bold text-[var(--admin-primary)]">مرحباً بعودتك</p>
          <h2 className="mt-2 text-2xl font-black text-[var(--admin-text)] md:text-3xl">
            أ. {user?.fullName || "المعلم"}
          </h2>
          <p className="mt-2 max-w-2xl text-sm leading-7 text-[var(--admin-muted)]">
            ابدأ بالإجابات التي تنتظر التصحيح، ثم انتقل إلى المحتوى والامتحانات العامة.
          </p>
        </section>

        {error ? (
          <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 text-sm font-bold text-[var(--admin-danger)]">
            <span>{error}</span>
            <button type="button" onClick={loadDashboardStats} className="min-h-11 rounded-lg px-4 underline underline-offset-4">
              إعادة المحاولة
            </button>
          </div>
        ) : null}

        {/* Stats Section */}
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-4" aria-busy={loading} aria-label={loading ? "جارٍ تحميل إحصاءات المعلم" : "إحصاءات المعلم"}>
          <AdminStatCard
            variant="light"
            icon={Users}
            label="الطلاب المشتركون"
            value={loading ? "—" : stats?.activeStudentsCount.toString() ?? "0"}
            subtitle="عدد الطلاب النشطين"
          />
          <AdminStatCard
            variant="accent"
            icon={BookOpen}
            label="الباقات الدراسية"
            value={loading ? "—" : stats?.packagesCount.toString() ?? "0"}
            subtitle="إجمالي الباقات النشطة"
          />
          <AdminStatCard
            variant="light"
            icon={Shield}
            label="الامتحانات"
            value={loading ? "—" : stats?.examsCount.toString() ?? "0"}
            subtitle="عدد الامتحانات المنشأة"
          />
          <AdminStatCard
            variant="muted"
            icon={GraduationCap}
            label="إجابات معلقة"
            value={loading ? "—" : stats?.pendingEssaysCount.toString() ?? "0"}
            subtitle="بانتظار تصحيح المعلم"
          />
        </div>

        <section aria-labelledby="package-sales-title" className="space-y-4">
          <div>
            <h2 id="package-sales-title" className="text-xl font-black text-[var(--admin-text)]">تفاصيل الباقات</h2>
            <p className="mt-1 text-sm text-[var(--admin-muted)]">عدد الطلاب المميزين حسب نوع المحتوى وطريقة الحصول عليه.</p>
          </div>
          {loading ? (
            <div className="grid gap-4 lg:grid-cols-2" aria-label="جارٍ تحميل تفاصيل الباقات">
              {[1, 2].map((placeholder) => (
                <div key={placeholder} className="h-40 animate-pulse rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]" />
              ))}
            </div>
          ) : stats?.packageSales?.length ? (
            <div className="grid gap-4 lg:grid-cols-2">
              {stats.packageSales.map((item) => (
                <article key={item.packageId} className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <h3 className="text-lg font-black text-[var(--admin-text)]">{item.packageName}</h3>
                    <div className="flex flex-wrap gap-2 text-xs font-black">
                      <span className="rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-[var(--admin-primary)]">شراء/تفعيل {item.purchasedStudents}</span>
                      <span className="rounded-full bg-[var(--admin-warning-10)] px-3 py-1.5 text-[var(--admin-warning)]">هدايا {item.giftStudents}</span>
                    </div>
                  </div>
                  <dl className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
                    {[
                      ["الباقة", item.packageBuyers],
                      ["الترم", item.termBuyers],
                      ["القسم / الشهر", item.sectionBuyers],
                      ["الحصة", item.lessonBuyers],
                    ].map(([label, value]) => (
                      <div key={label} className="rounded-xl bg-[var(--admin-card-soft)] p-3 text-center">
                        <dt className="text-xs font-bold text-[var(--admin-muted)]">{label}</dt>
                        <dd className="mt-1 text-2xl font-black text-[var(--admin-text)]">{value}</dd>
                      </div>
                    ))}
                  </dl>
                </article>
              ))}
            </div>
          ) : (
            <div className="rounded-2xl bg-[var(--admin-card)] p-6 text-sm font-bold text-[var(--admin-muted)]">لا توجد باقات لعرضها حالياً.</div>
          )}
        </section>

        <section aria-labelledby="teacher-actions-title" className="overflow-hidden rounded-2xl bg-[var(--admin-card)]">
          <div className="border-b border-[var(--admin-border)] px-5 py-4">
            <h2 id="teacher-actions-title" className="text-lg font-black text-[var(--admin-text)]">ابدأ العمل</h2>
          </div>
          {quickLinks.map(({ href, title, body, icon: Icon, count }) => (
            <Link key={href} href={href} className="group flex min-h-20 items-center gap-4 border-b border-[var(--admin-border)] px-5 py-4 last:border-b-0 hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)]">
              <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                <Icon className="h-5 w-5" aria-hidden="true" />
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <h3 className="font-black text-[var(--admin-text)]">{title}</h3>
                  {typeof count === "number" && count > 0 ? (
                    <span className="rounded-full bg-[var(--admin-warning-10)] px-2 py-0.5 text-xs font-black text-[var(--admin-warning)]">{count}</span>
                  ) : null}
                </div>
                <p className="mt-1 text-sm text-[var(--admin-muted)]">{body}</p>
              </div>
              <ArrowLeft className="h-5 w-5 text-[var(--admin-primary)] transition-transform group-hover:-translate-x-1" aria-hidden="true" />
            </Link>
          ))}
        </section>
      </div>
    </TeacherPage>
  );
}

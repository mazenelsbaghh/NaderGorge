"use client";

import Link from "next/link";
import { ArrowLeft, BookOpen, KeyRound, Shield, Sparkles } from "lucide-react";
import { useAuthStore } from "@/stores/auth-store";
import { AdminStatCard } from "@/components/admin";

export default function TeacherDashboardPage() {
  const { user } = useAuthStore();

  return (
    <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]">
      {/* Welcome Section */}
      <div className="relative overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_80%_10%,var(--admin-primary-15),transparent_42%)]" />
        <div className="relative flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary-15)] px-4 py-1 text-xs font-black text-[var(--admin-primary)]">
              <Sparkles className="h-3.5 w-3.5" />
              مرحباً بك في لوحة المعلم
            </div>
            <h1 className="mt-4 text-3xl font-black text-[var(--admin-text)] md:text-4xl">
              أهلاً، أ. {user?.fullName || "المعلم"}
            </h1>
            <p className="mt-2 text-sm text-[var(--admin-muted)]">
              هنا يمكنك إدارة باقاتك، توليد الأكواد للطلاب، وبناء بنك الأسئلة وتصحيح الامتحانات الخاصة بك.
            </p>
          </div>
        </div>
      </div>

      {/* Stats Section */}
      <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={BookOpen}
          label="الباقات الخاصة بك"
          value="إدارة"
          subtitle="الباقات والدروس والفيديوهات"
        />
        <AdminStatCard
          variant="accent"
          icon={KeyRound}
          label="أكواد الوصول"
          value="توليد"
          subtitle="توليد وتصدير أكواد الاشتراك"
        />
        <AdminStatCard
          variant="muted"
          icon={Shield}
          label="بنك الأسئلة"
          value="الامتحانات"
          subtitle="إنشاء الأسئلة وتصحيح المقالي"
        />
      </div>

      {/* Quick Access Grid */}
      <div className="grid grid-cols-1 gap-6 xl:grid-cols-3">
        {/* Packages Card */}
        <Link
          href="/teacher/packages"
          className="group relative overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl transition hover:bg-[var(--admin-hover)]"
        >
          <div className="flex items-start justify-between gap-4">
            <div>
              <h3 className="text-xl font-black text-[var(--admin-text)]">إدارة المحتوى</h3>
              <p className="mt-2 text-sm text-[var(--admin-muted)]">
                قم بإنشاء وتعديل باقاتك الدراسية، إضافة الدروس، وربط فيديوهات اليوتيوب أو VK.
              </p>
            </div>
            <div className="rounded-[1.25rem] bg-[var(--admin-primary)] p-4 text-[var(--admin-primary-contrast)] shadow-lg transition-transform group-hover:scale-110">
              <BookOpen className="h-6 w-6" />
            </div>
          </div>
          <div className="mt-8 flex items-center justify-between">
            <span className="rounded-full bg-[var(--admin-bg)] px-4 py-2 text-xs font-bold text-[var(--admin-primary)]">
              تصفح الباقات
            </span>
            <ArrowLeft className="h-5 w-5 text-[var(--admin-primary)] transition-transform group-hover:-translate-x-2" />
          </div>
        </Link>

        {/* Codes Card */}
        <Link
          href="/teacher/codes"
          className="group relative overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl transition hover:bg-[var(--admin-hover)]"
        >
          <div className="flex items-start justify-between gap-4">
            <div>
              <h3 className="text-xl font-black text-[var(--admin-text)]">أكواد الوصول</h3>
              <p className="mt-2 text-sm text-[var(--admin-muted)]">
                قم بتوليد الأكواد لتمكين الطلاب من تفعيل الباقات مجاناً وتتبع حالة استخدام كل كود.
              </p>
            </div>
            <div className="rounded-[1.25rem] bg-[var(--admin-primary)] p-4 text-[var(--admin-primary-contrast)] shadow-lg transition-transform group-hover:scale-110">
              <KeyRound className="h-6 w-6" />
            </div>
          </div>
          <div className="mt-8 flex items-center justify-between">
            <span className="rounded-full bg-[var(--admin-bg)] px-4 py-2 text-xs font-bold text-[var(--admin-primary)]">
              إدارة الأكواد
            </span>
            <ArrowLeft className="h-5 w-5 text-[var(--admin-primary)] transition-transform group-hover:-translate-x-2" />
          </div>
        </Link>

        {/* Exams Card */}
        <Link
          href="/teacher/exams"
          className="group relative overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-8 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl transition hover:bg-[var(--admin-hover)]"
        >
          <div className="flex items-start justify-between gap-4">
            <div>
              <h3 className="text-xl font-black text-[var(--admin-text)]">بنك الأسئلة والامتحانات</h3>
              <p className="mt-2 text-sm text-[var(--admin-muted)]">
                أنشئ اختبارات تفاعلية لدروسك، قم بصياغة أسئلة الاختيار من متعدد، المقالي، والخطأ، وصحح إجابات الطلاب.
              </p>
            </div>
            <div className="rounded-[1.25rem] bg-[var(--admin-primary)] p-4 text-[var(--admin-primary-contrast)] shadow-lg transition-transform group-hover:scale-110">
              <Shield className="h-6 w-6" />
            </div>
          </div>
          <div className="mt-8 flex items-center justify-between">
            <span className="rounded-full bg-[var(--admin-bg)] px-4 py-2 text-xs font-bold text-[var(--admin-primary)]">
              افتح بنك الأسئلة
            </span>
            <ArrowLeft className="h-5 w-5 text-[var(--admin-primary)] transition-transform group-hover:-translate-x-2" />
          </div>
        </Link>
      </div>
    </div>
  );
}

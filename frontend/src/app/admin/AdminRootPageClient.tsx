'use client';

import Link from 'next/link';
import { ArrowLeft } from 'lucide-react';

import {
  AdminShellChrome,
  AdminStatCard,
  ClockInOutWidget,
} from '@/components/admin';
import { adminRootLinks, adminRootStats } from '@/packages/admin';
import { useHasPermission } from '@/hooks/useHasPermission';

export default function AdminRootPageClient() {
  const { hasPermission } = useHasPermission();

  const getPermissionForHref = (href: string) => {
    if (href.startsWith('/admin/subjects')) return 'content.manage';
    if (href.startsWith('/admin/teachers')) return 'users.manage';
    if (href.startsWith('/admin/students')) return 'users.manage';
    if (href.startsWith('/admin/assistants')) return 'users.manage';
    if (href.startsWith('/admin/admins')) return 'users.manage';
    if (href.startsWith('/admin/content')) return hasPermission('content.manage') || hasPermission('comments.manage');
    if (href.startsWith('/admin/codes')) return 'codes.manage';
    if (href.startsWith('/admin/questions')) return 'exams.manage';
    if (href.startsWith('/admin/overrides')) return 'users.manage';
    if (href.startsWith('/admin/finance')) return 'users.manage';
    return null;
  };

  const filteredLinks = adminRootLinks.filter((item) => {
    const perm = getPermissionForHref(item.href);
    if (typeof perm === 'boolean') return perm;
    return !perm || hasPermission(perm);
  });

  return (
    <AdminShellChrome
      activePath="/admin"
      sectionLabel="لوحة الإدارة"
      pageTitle="الرئيسية"
      subtitle="بوابة الإدارة المركزية لكل أدوات النظام بدون أي تحويلات إلى صفحات الطالب أو المساعد."
    >
      <section className="mb-10 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={adminRootStats.structure}
          label="الهيكلة"
          value="5"
          subtitle="أقسام إدارة جاهزة ومتصلة ببعض"
        />

        <AdminStatCard
          variant="accent"
          icon={adminRootStats.ready}
          label="Admin"
          value="جاهز"
          subtitle="الروتر الداخلي إداري بالكامل"
        />

        <AdminStatCard
          variant="muted"
          icon={adminRootStats.route}
          label="المسار"
          value="1"
          subtitle="مسار رئيسي واضح يبدأ من /admin"
        />
      </section>

      <div className="mb-10">
        <ClockInOutWidget />
      </div>

      <section className="grid grid-cols-1 gap-6 xl:grid-cols-2">
        {filteredLinks.map((item) => {
          const Icon = item.icon;

          return (
            <Link
              key={item.href}
              href={item.href}
              className="group overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl transition hover:bg-[var(--admin-hover)]"
            >
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="text-2xl font-black text-[var(--admin-text)]">
                    {item.title}
                  </h2>
                  <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">
                    {item.body}
                  </p>
                </div>
                <div
                  className={`rounded-[1.25rem] bg-[var(--admin-primary)] p-4 text-[var(--admin-primary-contrast)] shadow-lg transition-transform group-hover:scale-110`}
                >
                  <Icon className="h-6 w-6" />
                </div>
              </div>

              <div className="mt-6 flex items-center justify-between">
                <span className="rounded-full bg-[var(--admin-bg)] px-4 py-2 text-xs font-bold text-[var(--admin-primary)]">
                  افتح القسم
                </span>
                <ArrowLeft className="h-5 w-5 text-[var(--admin-primary)] transition-transform group-hover:-translate-x-2" />
              </div>
            </Link>
          );
        })}
      </section>
    </AdminShellChrome>
  );
}

'use client';

import Link from 'next/link';
import { ArrowLeft, LayoutGrid } from 'lucide-react';

import {
  AdminShellChrome,
  ClockInOutWidget,
} from '@/components/admin';
import { adminRootLinks } from '@/packages/admin';
import { useHasPermission } from '@/hooks/useHasPermission';
import { useAuthStore } from '@/stores/auth-store';

export default function AdminRootPageClient() {
  const { hasPermission } = useHasPermission();
  const user = useAuthStore((state) => state.user);

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

  let filteredLinks = adminRootLinks.filter((item) => {
    const perm = getPermissionForHref(item.href);
    if (typeof perm === 'boolean') return perm;
    return !perm || hasPermission(perm);
  });

  const allowedNavbarItems = user?.allowedNavbarItems;
  if (allowedNavbarItems && allowedNavbarItems.length > 0) {
    filteredLinks = filteredLinks.filter((item) =>
      allowedNavbarItems.includes(item.href)
    );
  }

  return (
    <AdminShellChrome
      activePath="/admin"
      sectionLabel="لوحة الإدارة"
      pageTitle="الرئيسية"
      subtitle="الوصول المباشر إلى الأدوات المتاحة لك حسب صلاحيات حسابك."
    >
      <section className="mb-8 flex items-center justify-between gap-4 rounded-2xl bg-[var(--admin-card-soft)] p-5">
        <div>
          <p className="text-sm font-bold text-[var(--admin-muted)]">الأقسام المتاحة</p>
          <p className="mt-1 text-3xl font-black text-[var(--admin-text)]">{filteredLinks.length}</p>
        </div>
        <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
          <LayoutGrid className="h-6 w-6" aria-hidden="true" />
        </div>
      </section>

      <div className="mb-10">
        <ClockInOutWidget />
      </div>

      <section aria-labelledby="admin-sections-title" className="overflow-hidden rounded-2xl bg-[var(--admin-card)]">
        <div className="border-b border-[var(--admin-border)] px-5 py-4">
          <h2 id="admin-sections-title" className="text-lg font-black text-[var(--admin-text)]">
            أدوات الإدارة
          </h2>
          <p className="mt-1 text-sm text-[var(--admin-muted)]">
            اختر القسم المطلوب لبدء العمل.
          </p>
        </div>
        {filteredLinks.map((item) => {
          const Icon = item.icon;

          return (
            <Link
              key={item.href}
              href={item.href}
              prefetch={false}
              className="group flex min-h-20 items-center gap-4 border-b border-[var(--admin-border)] px-5 py-4 transition-colors last:border-b-0 hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)]"
            >
              <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                <Icon className="h-5 w-5" aria-hidden="true" />
              </div>
              <div className="min-w-0 flex-1">
                <h3 className="font-black text-[var(--admin-text)]">{item.title}</h3>
                <p className="mt-1 line-clamp-2 text-sm leading-6 text-[var(--admin-muted)]">{item.body}</p>
              </div>
              <ArrowLeft className="h-5 w-5 shrink-0 text-[var(--admin-primary)] transition-transform group-hover:-translate-x-1" aria-hidden="true" />
            </Link>
          );
        })}
      </section>
    </AdminShellChrome>
  );
}

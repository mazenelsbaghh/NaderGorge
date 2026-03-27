'use client';

import Link from 'next/link';
import {
  ArrowLeft,
  BookOpenText,
  KeyRound,
  Layers3,
  Shield,
  Sparkles,
  UserCog,
  Wrench,
  LucideIcon
} from 'lucide-react';

import { AdminShellChrome, AdminStatCard } from '@/components/admin';

interface AdminLink {
  href: string;
  title: string;
  body: string;
  icon: LucideIcon;
}

const adminLinks: AdminLink[] = [
  {
    href: '/admin/users',
    title: 'إدارة المستخدمين',
    body: 'الحسابات والصلاحيات والحالات وأجهزة الدخول.',
    icon: UserCog,
  },
  {
    href: '/admin/content',
    title: 'إدارة المحتوى',
    body: 'الباقات والأقسام والدروس والفيديوهات.',
    icon: BookOpenText,
  },
  {
    href: '/admin/codes',
    title: 'أكواد الوصول',
    body: 'توليد الأكواد وتتبع استخدامها وتصديرها.',
    icon: KeyRound,
  },
  {
    href: '/admin/questions',
    title: 'بنك الأسئلة',
    body: 'الأسئلة والاختيارات والنقاط والتصنيفات.',
    icon: Shield,
  },
  {
    href: '/admin/overrides',
    title: 'التعديلات اليدوية',
    body: 'فتح الدروس وتصفير المشاهدات للحالات الخاصة.',
    icon: Wrench,
  },
];

export default function AdminRootPage() {
  return (
    <AdminShellChrome
      activePath="/admin"
      sectionLabel="لوحة الإدارة"
      pageTitle="الرئيسية"
      subtitle="بوابة الإدارة المركزية لكل أدوات النظام بدون أي تحويلات إلى صفحات الطالب أو المساعد."
      action={
        <div className="flex flex-wrap items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 shadow-sm backdrop-blur-xl">
          {adminLinks.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className="rounded-full border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2 text-xs font-bold text-[var(--admin-primary)] transition hover:bg-[var(--admin-hover)]"
            >
              {item.title}
            </Link>
          ))}
        </div>
      }
    >
      <section className="mb-10 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={Layers3}
          label="الهيكلة"
          value="٥"
          subtitle="أقسام إدارة جاهزة ومتصلة ببعض"
        />

        <AdminStatCard
          variant="accent"
          icon={Sparkles}
          label="Admin"
          value="جاهز"
          subtitle="الروتر الداخلي إداري بالكامل"
        />

        <AdminStatCard
          variant="muted"
          icon={ArrowLeft}
          label="المسار"
          value="١"
          subtitle="مسار رئيسي واضح يبدأ من /admin"
        />
      </section>

      <section className="grid grid-cols-1 gap-6 xl:grid-cols-2">
        {adminLinks.map((item) => {
          const Icon = item.icon;

          return (
            <Link
              key={item.href}
              href={item.href}
              className="group overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-2xl transition hover:bg-[var(--admin-hover)]"
            >
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="text-2xl font-black text-[var(--admin-text)]">{item.title}</h2>
                  <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">{item.body}</p>
                </div>
                <div className={`rounded-[1.25rem] bg-[var(--admin-primary)] p-4 text-[var(--admin-primary-contrast)] shadow-lg transition-transform group-hover:scale-110`}>
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

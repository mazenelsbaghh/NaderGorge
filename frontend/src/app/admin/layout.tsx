'use client';

import { Navbar } from '@/components/layout/Navbar';
import { Sidebar } from '@/components/layout/Sidebar';
import { Breadcrumbs } from '@/components/layout/Breadcrumbs';

const adminMenuItems = [
  { label: 'Dashboard', href: '/admin', icon: '📊' },
  { label: 'Users', href: '/admin/users', icon: '👥' },
  { label: 'Content', href: '/admin/content', icon: '📚' },
  { label: 'Access Codes', href: '/admin/codes', icon: '🔐' },
  { label: 'Question Bank', href: '/admin/questions', icon: '❓' },
  { label: 'Overrides', href: '/admin/overrides', icon: '⚙️' },
];

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <Navbar />
      <div className="flex">
        <Sidebar items={adminMenuItems} title="Admin Panel" />
        <div className="flex-1">
          <Breadcrumbs />
          <main className="p-6">{children}</main>
        </div>
      </div>
    </>
  );
}

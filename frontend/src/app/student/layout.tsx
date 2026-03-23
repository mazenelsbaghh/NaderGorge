'use client';

import { Navbar } from '@/components/layout/Navbar';
import { Sidebar } from '@/components/layout/Sidebar';
import { Breadcrumbs } from '@/components/layout/Breadcrumbs';

const studentMenuItems = [
  { label: 'Dashboard', href: '/student', icon: '📊' },
  { label: 'My Packages', href: '/student/packages', icon: '📦' },
  { label: 'Redeem Code', href: '/student/code-redemption', icon: '🔑' },
];

export default function StudentLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <Navbar />
      <div className="flex">
        <Sidebar items={studentMenuItems} title="Student Portal" />
        <div className="flex-1">
          <Breadcrumbs />
          <main className="p-6">{children}</main>
        </div>
      </div>
    </>
  );
}

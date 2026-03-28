'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { ChevronLeft, Home } from 'lucide-react';
import { Fragment } from 'react';

const adminLabelMap: Record<string, string> = {
  admin: 'الرئيسية',
  content: 'المحتوى',
  packages: 'الكورسات',
  users: 'المستخدمين',
  codes: 'الأكواد',
  questions: 'بنك الأسئلة',
  overrides: 'التعديلات',
  terms: 'الترم',
  lessons: 'الدروس',
  sections: 'الوحدات',
  exams: 'الامتحانات',
  dashboard: 'لوحة التحكم',
  'add-question': 'إضافة سؤال',
  settings: 'الإعدادات',
};

export function AdminBreadcrumbs() {
  const pathname = usePathname();

  if (!pathname?.startsWith('/admin')) return null;

  const segments = pathname.split('/').filter(Boolean);

  // Only show breadcrumbs when we're 2+ levels deep (has meaningful parent trail)
  // Remove the last segment (current page) — it's already shown as pageTitle in the header
  const parentSegments = segments.slice(0, -1);
  if (parentSegments.length <= 1) return null;

  const getLabel = (segment: string, index: number): string => {
    if (adminLabelMap[segment]) return adminLabelMap[segment];

    const isUuid =
      /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
        segment,
      );

    if (isUuid) {
      const parent = segments[index - 1];
      if (parent === 'packages') return 'الكورس';
      if (parent === 'terms') return 'الترم';
      if (parent === 'lessons') return 'الدرس';
      if (parent === 'sections') return 'الوحدة';
      if (parent === 'exams') return 'الامتحان';
      if (parent === 'questions') return 'السؤال';
      return 'تفاصيل';
    }

    return segment.replace(/[-_]/g, ' ');
  };

  return (
    <nav aria-label="مسار التنقل" className="mb-4 flex items-center">
      <ol className="flex items-center gap-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/60 px-4 py-2 text-sm font-bold text-[var(--admin-muted)] shadow-sm backdrop-blur-sm">
        <li className="flex items-center">
          <Home className="h-4 w-4 shrink-0" />
        </li>

        {parentSegments.map((segment, index) => {
          const href = '/' + segments.slice(0, index + 1).join('/');
          const isLast = index === parentSegments.length - 1;
          const label = getLabel(segment, index);

          return (
            <Fragment key={href}>
              <li className="shrink-0">
                <ChevronLeft className="h-3 w-3 opacity-40" />
              </li>
              <li className="shrink-0">
                {isLast ? (
                  <span className="text-[var(--admin-text)]">{label}</span>
                ) : (
                  <Link
                    href={href}
                    className="transition-colors hover:text-[var(--admin-text)] hover:underline"
                  >
                    {label}
                  </Link>
                )}
              </li>
            </Fragment>
          );
        })}
      </ol>
    </nav>
  );
}

'use client';

import {
  BookOpen,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  Clapperboard,
  Layers3,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';

import {
  filterStudentQuickAccessItems,
  type StudentQuickAccessKind,
} from '@/lib/student-quick-access';
import type { QuickAccessItemDto } from '@/services/student-service';

interface QuickAccessPanelProps {
  accessItems: QuickAccessItemDto[];
}

type AccessSection = {
  kind: StudentQuickAccessKind;
  title: string;
  countLabel: string;
  emptyMessage: string;
  icon: LucideIcon;
};

const REQUIRED_ACCESS_SECTIONS: AccessSection[] = [
  {
    kind: 'term',
    title: 'ترماتي الدراسية',
    countLabel: 'ترم',
    emptyMessage: 'لا توجد ترمات مفعّلة بشكل مستقل.',
    icon: CalendarDays,
  },
  {
    kind: 'section',
    title: 'أقسامي الدراسية',
    countLabel: 'قسم',
    emptyMessage: 'لا توجد أقسام مفعّلة بشكل مستقل.',
    icon: Layers3,
  },
  {
    kind: 'lesson',
    title: 'حصصي الدراسية',
    countLabel: 'حصة',
    emptyMessage: 'لا توجد حصص مفعّلة بشكل مستقل.',
    icon: BookOpen,
  },
];

const VIDEO_ACCESS_SECTION: AccessSection = {
  kind: 'video',
  title: 'فيديوهاتي التعليمية',
  countLabel: 'فيديو',
  emptyMessage: 'لا توجد فيديوهات مفعّلة بشكل مستقل.',
  icon: Clapperboard,
};

export function QuickAccessPanel({ accessItems }: QuickAccessPanelProps) {
  const videoAccessItems = filterStudentQuickAccessItems(accessItems, 'video');
  const accessSections =
    videoAccessItems.length > 0
      ? [...REQUIRED_ACCESS_SECTIONS, VIDEO_ACCESS_SECTION]
      : REQUIRED_ACCESS_SECTIONS;

  return (
    <section aria-label="اشتراكات المحتوى المستقلة" className="space-y-3">
      {accessSections.map((section) => (
        <QuickAccessSection
          key={section.kind}
          section={section}
          accessItems={
            section.kind === 'video'
              ? videoAccessItems
              : filterStudentQuickAccessItems(accessItems, section.kind)
          }
        />
      ))}
    </section>
  );
}

function QuickAccessSection({
  section,
  accessItems,
}: {
  section: AccessSection;
  accessItems: QuickAccessItemDto[];
}) {
  const Icon = section.icon;

  return (
    <details
      className="group rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]"
      open={accessItems.length > 0}
    >
      <summary className="flex min-h-14 cursor-pointer list-none items-center gap-3 px-5 py-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)]">
        <Icon
          className="h-5 w-5 shrink-0 text-[var(--admin-primary)]"
          aria-hidden="true"
        />
        <span className="flex-1 font-black text-[var(--admin-text)]">
          {section.title}
        </span>
        <span className="text-xs font-bold text-[var(--admin-muted)]">
          {accessItems.length} {section.countLabel}
        </span>
        <ChevronDown
          className="h-4 w-4 shrink-0 text-[var(--admin-muted)] transition-transform group-open:rotate-180"
          aria-hidden="true"
        />
      </summary>

      <div className="border-t border-[var(--admin-border)] p-3 sm:p-4">
        {accessItems.length === 0 ? (
          <p className="px-2 py-3 text-sm font-medium text-[var(--admin-muted)]">
            {section.emptyMessage}
          </p>
        ) : (
          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
            {accessItems.map((accessItem) => (
              <Link
                key={`${accessItem.accessType}-${accessItem.url}`}
                href={accessItem.url}
                className="flex min-h-14 items-center gap-3 rounded-xl px-3 py-2 transition-colors hover:bg-[var(--admin-card-soft)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
              >
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-black text-[var(--admin-text)]">
                    {accessItem.title}
                  </span>
                  <span className="mt-1 block truncate text-xs text-[var(--admin-muted)]">
                    {accessItem.pathBreadcrumb}
                  </span>
                </span>
                <ChevronLeft
                  className="h-4 w-4 shrink-0 text-[var(--admin-primary)]"
                  aria-hidden="true"
                />
              </Link>
            ))}
          </div>
        )}
      </div>
    </details>
  );
}

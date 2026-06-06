"use client";

import { ChevronLeft, Calendar, Folder, FileText } from "lucide-react";
import Link from "next/link";
import { type QuickAccessItemDto } from "@/services/student-service";

interface QuickAccessPanelProps {
  items: QuickAccessItemDto[];
}

export function QuickAccessPanel({ items }: QuickAccessPanelProps) {
  if (!items || items.length === 0) return null;

  return (
    <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm sm:p-8">
      <div className="mb-6">
        <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">وصول سريع</p>
        <p className="mt-2 text-sm font-bold text-[var(--admin-muted)]">ارجع بسرعة إلى الدروس والمسارات المفتوحة لك الآن.</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {items.map((item, idx) => {
          // accessType: 1 = Term, 2 = Month, 3 = Lesson
          const Icon = item.accessType === 1 ? Calendar : item.accessType === 2 ? Folder : FileText;

          return (
            <Link
              key={`${item.url}-${idx}`}
              href={item.url}
              className="group flex flex-col gap-3 rounded-2xl bg-[var(--admin-bg)] p-5 text-right transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)]"
            >
              <div className="flex items-start gap-3">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                  <Icon className="h-5 w-5" />
                </div>
                <div className="min-w-0">
                  <h3 className="line-clamp-1 text-base font-bold text-[var(--admin-text)] transition-colors group-hover:text-[var(--admin-primary)]">
                    {item.title}
                  </h3>
                  <p className="mt-1 line-clamp-2 text-xs leading-5 text-[var(--admin-muted)]">
                    {item.pathBreadcrumb}
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-2 text-[11px] font-black text-[var(--admin-primary)]">
                <span>اذهب مباشرة</span>
                <ChevronLeft className="h-3 w-3 transition-transform group-hover:-translate-x-1" />
              </div>
            </Link>
          );
        })}
      </div>
    </div>
  );
}

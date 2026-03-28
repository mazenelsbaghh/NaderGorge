"use client";

import { ChevronLeft, Zap, Calendar, Folder, FileText } from "lucide-react";
import Link from "next/link";
import { type QuickAccessItemDto } from "@/services/student-service";

interface QuickAccessPanelProps {
  items: QuickAccessItemDto[];
}

export function QuickAccessPanel({ items }: QuickAccessPanelProps) {
  if (!items || items.length === 0) return null;

  return (
    <div className="rounded-[32px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 sm:p-8 backdrop-blur-xl">
      <div className="mb-6 flex items-center gap-3">
        <div className="flex h-12 w-12 items-center justify-center rounded-[14px] bg-yellow-500/10 text-yellow-600">
          <Zap className="h-6 w-6" />
        </div>
        <div>
          <h2 className="text-xl font-black text-[var(--admin-text)]">وصول سريع</h2>
          <p className="text-sm font-bold text-[var(--admin-muted)]">اختصارات للمحتوى المفتوح جزئياً</p>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {items.map((item, idx) => {
          // accessType: 1 = Term, 2 = Month, 3 = Lesson
          const Icon = item.accessType === 1 ? Calendar : item.accessType === 2 ? Folder : FileText;

          return (
            <Link
              key={`${item.url}-${idx}`}
              href={item.url}
              className="group flex flex-col justify-between rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-5 transition-all hover:-translate-y-1 hover:border-[var(--admin-primary)]/50 hover:bg-[var(--admin-card-strong)] hover:shadow-lg hover:shadow-[var(--admin-primary)]/5 text-right relative overflow-hidden"
            >
               <div className="absolute -left-4 -top-4 text-[var(--admin-primary)] opacity-5 transition-transform group-hover:scale-110 group-hover:opacity-10">
                 <Icon className="h-24 w-24" />
               </div>
              <div>
                <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                  <Icon className="h-5 w-5" />
                </div>
                <h3 className="line-clamp-1 text-lg font-bold text-[var(--admin-text)] transition-colors group-hover:text-[var(--admin-primary)]">
                  {item.title}
                </h3>
                <p className="mt-1 line-clamp-2 text-xs leading-5 text-[var(--admin-muted)]">
                  {item.pathBreadcrumb}
                </p>
              </div>

              <div className="mt-4 flex items-center gap-2 text-[11px] font-black text-[var(--admin-primary)]">
                <span>انتقال سريع</span>
                <ChevronLeft className="h-3 w-3 transition-transform group-hover:-translate-x-1" />
              </div>
            </Link>
          );
        })}
      </div>
    </div>
  );
}

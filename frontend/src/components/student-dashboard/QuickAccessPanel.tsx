"use client";

import { Calendar, ChevronDown, ChevronLeft, FileText, Folder } from "lucide-react";
import Link from "next/link";

import type { QuickAccessItemDto } from "@/services/student-service";

interface QuickAccessPanelProps {
  items: QuickAccessItemDto[];
}

export function QuickAccessPanel({ items }: QuickAccessPanelProps) {
  if (items.length === 0) return null;

  return (
    <details className="group rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
      <summary className="flex min-h-14 cursor-pointer list-none items-center justify-between gap-4 rounded-2xl px-5 py-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)] sm:px-6">
        <span>
          <span className="block text-base font-black text-[var(--admin-text)]">وصول سريع</span>
          <span className="mt-0.5 block text-xs text-[var(--admin-muted)]">
            {items.length} روابط مفتوحة لك
          </span>
        </span>
        <ChevronDown className="h-5 w-5 text-[var(--admin-primary)] transition-transform group-open:rotate-180" />
      </summary>

      <div className="grid gap-2 border-t border-[var(--admin-border)] p-3 sm:grid-cols-2 sm:p-4 lg:grid-cols-3">
        {items.map((item, index) => {
          const Icon = item.accessType === 1 ? Calendar : item.accessType === 2 ? Folder : FileText;

          return (
            <Link
              key={`${item.url}-${index}`}
              href={item.url}
              className="flex min-h-12 items-center gap-3 rounded-xl px-3 py-2 transition-colors hover:bg-[var(--admin-card-soft)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
            >
              <Icon className="h-5 w-5 shrink-0 text-[var(--admin-primary)]" />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-bold text-[var(--admin-text)]">{item.title}</span>
                <span className="mt-0.5 block truncate text-xs text-[var(--admin-muted)]">
                  {item.pathBreadcrumb}
                </span>
              </span>
              <ChevronLeft className="h-4 w-4 shrink-0 text-[var(--admin-primary)]" />
            </Link>
          );
        })}
      </div>
    </details>
  );
}

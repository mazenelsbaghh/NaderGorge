"use client";

import Link from "next/link";
import { ArrowUpLeft, BookMarked, Bug, ChevronDown, KeyRound, Wallet } from "lucide-react";

const destinations = [
  {
    href: "/student/packages",
    title: "الباقات الدراسية",
    description: "راجع كل الباقات المتاحة والمفعلة.",
    icon: BookMarked,
  },
  {
    href: "/student/balance",
    title: "المحفظة",
    description: "راجع الرصيد وسجل الشحن والشراء.",
    icon: Wallet,
  },
  {
    href: "/student/code-redemption",
    title: "تفعيل الكود",
    description: "فعّل باقة جديدة أو أضف رصيدًا.",
    icon: KeyRound,
  },
  {
    href: "/student/mistakes",
    title: "ملف الأخطاء",
    description: "ارجع للأسئلة التي تحتاج مراجعة.",
    icon: Bug,
  },
];

export function StudentDestinationsPanel() {
  return (
    <details className="group rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
      <summary className="flex min-h-14 cursor-pointer list-none items-center justify-between gap-4 rounded-2xl px-5 py-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)] sm:px-6">
        <span>
          <span className="block text-base font-black text-[var(--admin-text)]">روابط وخدمات</span>
          <span className="mt-0.5 block text-xs text-[var(--admin-muted)]">المحفظة، الأكواد، والأخطاء</span>
        </span>
        <ChevronDown className="h-5 w-5 text-[var(--admin-primary)] transition-transform group-open:rotate-180" />
      </summary>

      <div className="grid gap-2 border-t border-[var(--admin-border)] p-3 sm:grid-cols-2 sm:p-4">
        {destinations.map((destination) => {
          const Icon = destination.icon;

          return (
            <Link
              key={destination.href}
              href={destination.href}
              className="flex min-h-14 items-center gap-3 rounded-xl px-3 py-2 transition-colors hover:bg-[var(--admin-card-soft)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
            >
              <Icon className="h-5 w-5 shrink-0 text-[var(--admin-primary)]" />
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-black text-[var(--admin-text)]">{destination.title}</span>
                <span className="mt-0.5 block text-xs leading-5 text-[var(--admin-muted)]">
                  {destination.description}
                </span>
              </span>
              <ArrowUpLeft className="h-4 w-4 shrink-0 text-[var(--admin-primary)]" />
            </Link>
          );
        })}
      </div>
    </details>
  );
}

"use client";

import type { ReactNode } from "react";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { cn } from "@/lib/utils";

type StudentBottomNavItem = {
  label: string;
  href: string;
  icon: ReactNode;
};

export function StudentBottomNav({ items }: { items: StudentBottomNavItem[] }) {
  const pathname = usePathname();

  return (
    <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-[var(--landing-line)] bg-[var(--landing-bg-soft)]/95 px-2 py-2 backdrop-blur md:hidden">
      <div className="mx-auto grid w-full max-w-xl grid-cols-3 gap-2 rounded-[26px] border border-[var(--landing-line)] bg-[var(--landing-card)] p-2 shadow-[0_-18px_40px_var(--admin-shadow)]">
        {items.map((item) => {
          const isActive = pathname === item.href;

          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "flex min-w-0 flex-col items-center justify-center gap-1 rounded-[20px] px-1.5 py-2.5 text-center text-xs font-black transition-all",
                isActive
                  ? "bg-[var(--landing-card-strong)] text-[var(--landing-accent)]"
                  : "text-[var(--landing-muted)]",
              )}
            >
              <span className="flex h-9 w-9 items-center justify-center rounded-2xl bg-[var(--landing-card-strong)]">
                {item.icon}
              </span>
              <span className="truncate">{item.label}</span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
}

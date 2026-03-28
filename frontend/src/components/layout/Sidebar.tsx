"use client";

import type { ReactNode } from "react";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { GraduationCap } from "lucide-react";

import { cn } from "@/lib/utils";
import { GamificationWidget } from "@/components/student-dashboard/GamificationWidget";

interface SidebarItem {
  label: string;
  href: string;
  icon: ReactNode;
}

interface SidebarProps {
  items: SidebarItem[];
  title?: string;
}

export function Sidebar({ items, title }: SidebarProps) {
  const pathname = usePathname();

  return (
    <aside className="sticky top-0 hidden h-screen w-72 shrink-0 flex-col border-e border-[var(--landing-line)] bg-[color:rgba(255,249,239,0.76)] px-4 py-5 backdrop-blur md:flex">
      {title && (
        <div className="landing-panel mb-4 rounded-[28px] p-4">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--landing-card-strong)] text-[var(--landing-accent)]">
              <GraduationCap className="h-6 w-6" />
            </div>
            <div>
              <p className="text-xs font-black tracking-[0.26em] text-[var(--landing-muted)]">
                STUDENT AREA
              </p>
              <h2 className="mt-1 text-lg font-black text-[var(--landing-ink)]">{title}</h2>
            </div>
          </div>
        </div>
      )}

      <nav className="landing-panel flex-1 space-y-2 rounded-[30px] p-3">
        {items.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            aria-current={pathname === item.href ? "page" : undefined}
            className={cn(
              "flex items-center gap-3 rounded-2xl px-4 py-3.5 text-sm font-bold transition-all",
              pathname === item.href
                ? "bg-[var(--landing-card-strong)] text-[var(--landing-accent)] shadow-[0_10px_24px_rgba(88,55,18,0.08)]"
                : "text-[var(--landing-muted)] hover:bg-[var(--landing-bg-soft)] hover:text-[var(--landing-ink)]"
            )}
          >
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-white/70">
              {item.icon}
            </span>
            <span>{item.label}</span>
          </Link>
        ))}
      </nav>

      <GamificationWidget />
    </aside>
  );
}

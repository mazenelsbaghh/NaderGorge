import { BadgeCheck, BookMarked, ChevronDown, KeyRound } from "lucide-react";

import type { DashboardDto } from "@/services/student-service";

const statItems = [
  {
    key: "packages",
    label: "الباقات المفعلة",
    icon: BookMarked,
    getValue: (data: DashboardDto) => data.activePackages.length,
  },
  {
    key: "lessons",
    label: "الدروس المكتملة",
    icon: BadgeCheck,
    getValue: (data: DashboardDto) => `${data.totalLessonsCompleted}/${data.totalLessons}`,
  },
  {
    key: "codes",
    label: "الأكواد المستخدمة",
    icon: KeyRound,
    getValue: (data: DashboardDto) => data.codesRedeemed,
  },
];

export function StatsStrip({ data }: { data: DashboardDto }) {
  return (
    <details className="group rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
      <summary className="flex min-h-14 cursor-pointer list-none items-center justify-between gap-4 rounded-2xl px-5 py-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)] sm:px-6">
        <span>
          <span className="block text-base font-black text-[var(--admin-text)]">أرقام حسابك</span>
          <span className="mt-0.5 block text-xs text-[var(--admin-muted)]">ملخص إضافي عند الحاجة</span>
        </span>
        <ChevronDown className="h-5 w-5 text-[var(--admin-primary)] transition-transform group-open:rotate-180" />
      </summary>

      <div className="grid gap-px border-t border-[var(--admin-border)] bg-[var(--admin-border)] sm:grid-cols-3">
        {statItems.map((item) => {
          const Icon = item.icon;

          return (
            <div key={item.key} className="flex items-center justify-between gap-3 bg-[var(--admin-card)] p-4">
              <div>
                <p className="text-xs font-bold text-[var(--admin-muted)]">{item.label}</p>
                <p className="mt-1 text-xl font-black text-[var(--admin-text)]">{item.getValue(data)}</p>
              </div>
              <Icon className="h-5 w-5 text-[var(--admin-primary)]" />
            </div>
          );
        })}
      </div>
    </details>
  );
}

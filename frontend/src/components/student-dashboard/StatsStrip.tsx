import {
  BadgeCheck,
  BookMarked,
  ChartColumnIncreasing,
} from "lucide-react";
import { motion } from "framer-motion";
import { staggeredCard } from "@/lib/motion";

import type { DashboardDto } from "@/services/student-service";

const statItems = [
  {
    key: "packages",
    label: "باقاتك الشغالة",
    icon: BookMarked,
    getValue: (data: DashboardDto) => data.activePackages.length,
  },
  {
    key: "lessons",
    label: "دروس خلصتها",
    icon: BadgeCheck,
    getValue: (data: DashboardDto) => `${data.totalLessonsCompleted}/${data.totalLessons}`,
  },
  {
    key: "progress",
    label: "نسبة تقدمك",
    icon: ChartColumnIncreasing,
    getValue: (data: DashboardDto) => `${data.overallProgressPercent}%`,
  },
];

const cardVariant = staggeredCard(0.08);

export function StatsStrip({ data }: { data: DashboardDto }) {
  return (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm sm:p-8">
      <div className="mb-5 flex flex-col gap-2">
        <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
          إنت فين دلوقتي
        </p>
        <p className="text-sm leading-7 text-[var(--admin-muted)]">
          أرقام سريعة توريك وضعك من غير ما تفتح صفحات تانية.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-3 md:gap-0 md:divide-x md:divide-[var(--admin-border)]">
      {statItems.map((item, i) => {
        const Icon = item.icon;

        return (
          <motion.div
            key={item.key}
            custom={i}
            variants={cardVariant}
            initial="hidden"
            animate="show"
            className="flex items-start justify-between gap-4 rounded-[24px] bg-[var(--admin-card-soft)] p-5 md:rounded-none md:bg-transparent md:px-6 md:py-4"
          >
            <div>
              <p className="text-sm font-bold text-[var(--admin-muted)]">{item.label}</p>
              <p className="mt-3 text-3xl font-black text-[var(--admin-text)]">
                {item.getValue(data)}
              </p>
            </div>

            <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
              <Icon className="h-5 w-5" />
            </div>
          </motion.div>
        );
      })}
      </div>
    </section>
  );
}

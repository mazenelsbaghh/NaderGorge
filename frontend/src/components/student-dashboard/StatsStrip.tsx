import {
  BadgeCheck,
  BookMarked,
  ChartColumnIncreasing,
  Ticket,
} from "lucide-react";
import { motion } from "framer-motion";
import { staggeredCard } from "@/lib/motion";

import type { DashboardDto } from "@/services/student-service";

const statItems = [
  {
    key: "packages",
    label: "الباقات النشطة",
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
    key: "progress",
    label: "نسبة التقدم",
    icon: ChartColumnIncreasing,
    getValue: (data: DashboardDto) => `${data.overallProgressPercent}%`,
  },
  {
    key: "codes",
    label: "الأكواد المفعّلة",
    icon: Ticket,
    getValue: (data: DashboardDto) => data.codesRedeemed,
  },
];

const cardVariant = staggeredCard(0.08);

export function StatsStrip({ data }: { data: DashboardDto }) {
  return (
    <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
      {statItems.map((item, i) => {
        const Icon = item.icon;

        return (
          <motion.article
            key={item.key}
            custom={i}
            variants={cardVariant}
            initial="hidden"
            animate="show"
            className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] backdrop-blur-xl p-5 transition hover:-translate-y-0.5 hover:shadow-[0_16px_36px_rgba(88,55,18,0.12)]"
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-sm font-bold text-[var(--admin-muted)]">{item.label}</p>
                <p className="mt-3 text-3xl font-black text-[var(--admin-text)]">
                  {item.getValue(data)}
                </p>
              </div>
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
                <Icon className="h-6 w-6" />
              </div>
            </div>
          </motion.article>
        );
      })}
    </section>
  );
}

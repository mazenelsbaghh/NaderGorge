import { BadgeCheck, KeyRound, ShieldCheck } from "lucide-react";
import type { ReactNode } from "react";

import type { PackageCodePageDto } from "@/services/content-service";

const THEME_STYLES: Record<string, string> = {
  "default-gold": "from-[var(--admin-primary)]/12 via-[var(--admin-card)] to-[var(--admin-card-strong)]",
  "physics-gold": "from-amber-500/15 via-[var(--admin-card)] to-orange-500/10",
  "emerald-accent": "from-emerald-500/15 via-[var(--admin-card)] to-teal-500/10",
  "ocean-accent": "from-sky-500/15 via-[var(--admin-card)] to-cyan-500/10",
};

export function PackageCodeRedemptionShowcase({ page }: { page: PackageCodePageDto }) {
  const gradient = THEME_STYLES[page.themeAccentKey] ?? THEME_STYLES["default-gold"];

  return (
    <section className={`rounded-[28px] bg-gradient-to-br ${gradient} p-5 shadow-[0_24px_60px_var(--admin-shadow)] sm:rounded-[32px] sm:p-7 md:p-9`}>
      <div className="grid gap-6 lg:grid-cols-[1.15fr_0.85fr] lg:items-start">
        <div>
          <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--admin-card)]/75 text-[var(--admin-primary)] backdrop-blur">
            <KeyRound className="h-5 w-5" />
          </div>
          <p className="mt-4 text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
            {page.hero.eyebrow}
          </p>
          <h1 className="mt-3 text-2xl font-black text-[var(--admin-text)] sm:text-3xl md:text-5xl">
            {page.hero.title}
          </h1>
          <p className="mt-4 max-w-2xl text-sm leading-7 text-[var(--admin-muted)] sm:text-base md:text-lg">
            {page.hero.description}
          </p>
        </div>

        <div className="space-y-4">
          <PanelCard icon={<BadgeCheck className="h-4 w-4" />} title={page.offerPanel.title} description={page.offerPanel.description} />
          <PanelCard icon={<ShieldCheck className="h-4 w-4" />} title={page.supportPanel.title} description={page.supportPanel.description} />
        </div>
      </div>
    </section>
  );
}

function PanelCard({
  icon,
  title,
  description,
}: {
  icon: ReactNode;
  title: string;
  description: string;
}) {
  return (
    <div className="rounded-[24px] bg-[var(--admin-card)]/80 p-5 backdrop-blur">
      <div className="flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
        <span className="text-[var(--admin-primary)]">{icon}</span>
        <span>{title}</span>
      </div>
      <p className="mt-3 text-sm leading-7 text-[var(--admin-muted)]">{description}</p>
    </div>
  );
}

"use client";

import Link from "next/link";
import { ArrowUpLeft, BookMarked, Bug, KeyRound, Wallet } from "lucide-react";

const destinations = [
  {
    href: "/student/packages",
    title: "الباقات الدراسية",
    description: "شاهد الباقات المفعلة لديك، وتعرّف على الباقات التي ما زالت تحتاج تفعيلًا.",
    icon: BookMarked,
  },
  {
    href: "/student/balance",
    title: "المحفظة",
    description: "راجع رصيدك وسجل الشحن والشراء قبل أي خطوة جديدة.",
    icon: Wallet,
  },
  {
    href: "/student/code-redemption",
    title: "تفعيل الكود",
    description: "أدخل كودًا جديدًا لفتح باقة أو إضافة رصيد إلى محفظتك.",
    icon: KeyRound,
  },
  {
    href: "/student/mistakes",
    title: "ملف الأخطاء",
    description: "راجع الأسئلة والواجبات التي تحتاج منك إعادة تركيز.",
    icon: Bug,
  },
];

export function StudentDestinationsPanel() {
  return (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm sm:p-8">
      <div className="mb-6 flex flex-col gap-2">
        <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
          أماكنك الأساسية
        </p>
        <p className="max-w-2xl text-sm leading-7 text-[var(--admin-muted)]">
          الصفحات التي ستحتاجها أكثر من مرة أثناء المذاكرة.
        </p>
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        {destinations.map((destination) => {
          const Icon = destination.icon;

          return (
            <Link
              key={destination.href}
              href={destination.href}
              className="group flex min-w-0 items-center justify-between gap-4 rounded-[22px] bg-[var(--admin-card-soft)] px-4 py-4 transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
            >
              <div className="flex min-w-0 items-center gap-4">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                  <Icon className="h-5 w-5" />
                </div>
                <div className="min-w-0">
                  <h3 className="text-base font-black text-[var(--admin-text)]">
                    {destination.title}
                  </h3>
                  <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
                    {destination.description}
                  </p>
                </div>
              </div>
              <ArrowUpLeft className="h-4 w-4 shrink-0 text-[var(--admin-primary)] transition-transform group-hover:-translate-x-0.5 group-hover:-translate-y-0.5" />
            </Link>
          );
        })}
      </div>
    </section>
  );
}

"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ChevronLeft, House } from "lucide-react";

const labelMap: Record<string, string> = {
  student: "الطالب",
  packages: "الباقات",
  "code-redemption": "تفعيل كود",
  admin: "الإدارة",
  content: "المحتوى",
  codes: "الأكواد",
  users: "المستخدمون",
  questions: "الأسئلة",
  overrides: "التعديلات",
  login: "تسجيل الدخول",
  register: "إنشاء حساب",
  about: "عن المنصة",
  faq: "الأسئلة الشائعة",
};

export function Breadcrumbs() {
  const pathname = usePathname();
  const segments = pathname.split("/").filter(Boolean);

  if (segments.length <= 1) return null;

  const getLabel = (segment: string, index: number) => {
    if (labelMap[segment]) return labelMap[segment];

    const isUuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      segment,
    );

    if (isUuid) {
      const previousSegment = segments[index - 1];
      if (previousSegment === "packages") return "تفاصيل الباقة";
      if (previousSegment === "lessons") return "الدرس";
      if (previousSegment === "exams") return "الامتحان";
      return "تفاصيل";
    }

    return segment.replace(/[-_]/g, " ").replace(/^\(|\)$/g, "");
  };

  return (
    <nav className="landing-panel flex max-w-full flex-wrap items-center gap-2 overflow-hidden rounded-[24px] px-3 py-2.5 text-sm text-[var(--landing-muted)] sm:rounded-full sm:px-4">
      <Link href="/" className="flex shrink-0 items-center gap-2 font-bold transition hover:text-[var(--landing-accent)]">
        <House className="h-4 w-4" />
        <span>الرئيسية</span>
      </Link>
      {segments.map((segment, index) => {
        const href = "/" + segments.slice(0, index + 1).join("/");
        const isLast = index === segments.length - 1;
        const label = getLabel(segment, index);

        return (
          <span key={href} className="flex min-w-0 items-center gap-2">
            <ChevronLeft className="h-4 w-4 shrink-0 text-[var(--landing-muted)]/50" />
            {isLast ? (
              <span className="truncate font-black text-[var(--landing-ink)]">{label}</span>
            ) : (
              <Link
                href={href}
                className="truncate font-semibold transition hover:text-[var(--landing-accent)]"
              >
                {label}
              </Link>
            )}
          </span>
        );
      })}
    </nav>
  );
}

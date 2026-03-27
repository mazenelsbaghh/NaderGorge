'use client';

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuthStore } from "@/stores/auth-store";
import { useRouter } from "next/navigation";

export function GlobalNav() {
  const pathname = usePathname();
  const { user, isAuthenticated, clearAuth } = useAuthStore();
  const router = useRouter();
  const isLanding = pathname === "/";
  const isStudentArea = pathname.startsWith("/student");
  const isAdminArea = pathname.startsWith("/admin");
  const isAuthRoute = pathname === "/login" || pathname === "/register";

  if (isStudentArea || isAdminArea || isAuthRoute) {
    return null;
  }

  const handleLogout = () => {
    clearAuth();
    router.push("/login");
  };

  // Navigation links based on auth state
  const navLinks = isAuthenticated
    ? user?.roles?.includes("Admin") || user?.roles?.includes("Teacher") || user?.roles?.includes("Assistant")
      ? [
          { href: "/admin/users", label: "المستخدمين" },
          { href: "/admin/content", label: "المحتوى" },
          { href: "/admin/codes", label: "الأكواد" },
          { href: "/admin/questions", label: "بنك الأسئلة" },
        ]
      : [
          { href: "/student", label: "لوحة التحكم" },
          { href: "/student/packages", label: "باقاتي" },
          { href: "/student/code-redemption", label: "تفعيل كود" },
        ]
    : isLanding
      ? [
          { href: "#features", label: "مميزات التجربة" },
          { href: "#subjects", label: "فروع الدراسة" },
          { href: "#testimonials", label: "آراء الطلبة" },
        ]
      : [];

  return (
    <header
      className={`z-50 px-4 py-3 md:px-0 ${
        isLanding ? "absolute inset-x-0 top-0" : "sticky top-0"
      }`}
    >
      <div
        className={`mx-auto flex w-[min(1180px,92vw)] items-center justify-between rounded-full px-4 py-3 backdrop-blur-xl md:px-6 ${
          isLanding
            ? "border border-white/30 bg-[color:rgba(255,248,236,0.34)] shadow-[0_18px_40px_rgba(88,55,18,0.10)]"
            : "border border-[var(--landing-line)] bg-[color:rgba(250,242,226,0.84)] shadow-[0_18px_40px_rgba(88,55,18,0.08)]"
        }`}
      >
        <Link href="/" className="flex items-center gap-3">
          <div
            className={`flex h-11 w-11 items-center justify-center rounded-full text-lg text-[var(--landing-accent)] shadow-inner ${
              isLanding
                ? "border border-white/35 bg-[color:rgba(255,248,236,0.28)]"
                : "border border-[var(--landing-line)] bg-[var(--landing-card)]"
            }`}
          >
            ☥
          </div>
          <div>
            <p className="text-xs font-semibold tracking-[0.3em] text-[var(--landing-muted)]">
              NADER GORGE
            </p>
            <p className="text-base font-black text-[var(--landing-ink)] md:text-lg">
              الأستاذ نادر جورج
            </p>
          </div>
        </Link>

        <nav className="hidden items-center gap-6 md:flex">
          {navLinks.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className={`text-sm font-bold transition hover:text-[var(--landing-accent)] ${
                pathname === link.href
                  ? "text-[var(--landing-accent)]"
                  : "text-[var(--landing-muted)]"
              }`}
            >
              {link.label}
            </Link>
          ))}
        </nav>

        <div className="flex items-center gap-3">
          {isAuthenticated ? (
            <>
              <span className="hidden text-sm font-bold text-[var(--landing-muted)] md:inline">
                {user?.fullName}
              </span>
              <button
                onClick={handleLogout}
                className={`rounded-full px-5 py-2.5 text-sm font-bold text-red-600 transition ${
                  isLanding
                    ? "border border-white/35 hover:bg-white/20"
                    : "border border-[var(--landing-line)] hover:bg-red-50"
                }`}
              >
                خروج
              </button>
            </>
          ) : (
            <>
              <Link
                href="/login"
                className={`hidden rounded-full px-5 py-2.5 text-sm font-bold text-[var(--landing-ink)] transition md:inline-flex ${
                  isLanding
                    ? "border border-white/35 bg-[color:rgba(255,248,236,0.18)] hover:bg-white/24"
                    : "border border-[var(--landing-line)] hover:bg-[var(--landing-card)]"
                }`}
              >
                تسجيل الدخول
              </Link>
              <Link
                href="/register"
                className="rounded-full bg-[var(--landing-accent)] px-5 py-2.5 text-sm font-extrabold text-[var(--landing-accent-foreground)] shadow-[0_10px_24px_rgba(145,95,42,0.28)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-accent-strong)]"
              >
                احجز مكانك
              </Link>
            </>
          )}
        </div>
      </div>
    </header>
  );
}

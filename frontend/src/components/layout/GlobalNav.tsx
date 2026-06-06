'use client';

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuthStore } from "@/stores/auth-store";
import { useRouter } from "next/navigation";
import { useState } from "react";
import {
  Navbar,
  NavBody,
  NavItems,
  MobileNav,
  NavbarLogo,
  NavbarButton,
  MobileNavHeader,
  MobileNavToggle,
  MobileNavMenu,
} from "@/components/ui/resizable-navbar";
import { AnimatedThemeToggler } from "@/components/ui/animated-theme-toggler";
import { useAdminTheme } from "@/components/admin/useAdminTheme";
import { motion, useReducedMotion } from "framer-motion";
import { ShinyButton } from "@/components/ui/shiny-button";
import { PlatformLogo } from "@/components/shared/PlatformLogo";
import { InteractiveHoverButton } from "@/components/ui/interactive-hover-button";

function LoginNavButtonContent() {
  const shouldReduceMotion = useReducedMotion();

  return (
    <span className="inline-flex items-center gap-2.5 whitespace-nowrap">
      <motion.span
        className="inline-flex h-4 w-4 shrink-0 origin-center drop-shadow-[0_2px_10px_rgba(145,95,42,0.2)]"
        animate={
          shouldReduceMotion
            ? undefined
            : {
                y: [0, -1.5, 0],
                rotate: [0, -5, 0],
                scale: [1, 1.06, 1],
              }
        }
        transition={{
          duration: 3.2,
          ease: 'easeInOut',
          repeat: Infinity,
        }}
      >
        <PlatformLogo variant="mark" size="sm" />
      </motion.span>
      <span>تسجيل الدخول</span>
    </span>
  );
}

export function GlobalNav() {
  const pathname = usePathname();
  const { user, isAuthenticated, clearAuth } = useAuthStore();
  const router = useRouter();
  const { isDark, toggleTheme } = useAdminTheme();
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  
  const isLanding = pathname === "/";
  const isStudentArea = pathname.startsWith("/student");
  const isAdminArea = pathname.startsWith("/admin");
  const isAuthRoute = pathname === "/login" || pathname === "/register" || pathname === "/forgot-password";
  const isFormsPage = pathname.startsWith("/forms");

  if (isStudentArea || isAdminArea || isAuthRoute || isFormsPage) {
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
          { href: "/", label: "الرئيسية" },
          { href: "#courses", label: "الدورات" },
          { href: "#teachers", label: "المعلمون" },
          { href: "#about-platform", label: "عن المنصة" },
          { href: "#testimonials", label: "آراء الطلبة" },
        ]
      : [];

  return (
    <div className={`z-50 w-full ${isLanding ? "absolute inset-x-0 top-0" : "sticky top-0"}`}>
      <Navbar isLanding={isLanding}>
        {/* Desktop Navigation */}
        <NavBody isLanding={isLanding}>
          <div className="flex items-center gap-3">
            <NavbarLogo />
          </div>
          
          <NavItems items={navLinks.map((link) => ({ name: link.label, link: link.href }))} />

          <div className="flex items-center gap-3">
            {isAuthenticated ? (
              <>
                <span className="hidden text-sm font-bold text-[var(--landing-muted)] md:inline">
                  {user?.fullName}
                </span>
                <NavbarButton
                  onClick={handleLogout}
                  variant="danger"
                  className="hidden md:inline-flex"
                >
                  خروج
                </NavbarButton>
              </>
            ) : (
              <>
                <ShinyButton href="/login" className="hidden md:inline-flex text-[15px] h-[46px] items-center px-8">
                  <LoginNavButtonContent />
                </ShinyButton>
                <InteractiveHoverButton href="/register" className="hidden md:inline-flex text-[15px] h-[46px] items-center px-6">
                  احجز مكانك
                </InteractiveHoverButton>
              </>
            )}

            {/* Theme Toggler right after the main actions */}
            <div className="flex h-10 items-center">
              <AnimatedThemeToggler
                checked={isDark}
                onToggle={toggleTheme}
                aria-label={isDark ? "التحول إلى الوضع الفاتح" : "التحول إلى الوضع الداكن"}
                title={isDark ? "التحول إلى الوضع الفاتح" : "التحول إلى الوضع الداكن"}
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full transition border border-[var(--landing-line)] text-[var(--landing-ink)] hover:bg-[var(--landing-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--landing-accent)]"
              />
            </div>
          </div>
        </NavBody>

        {/* Mobile Navigation */}
        <MobileNav>
          <MobileNavHeader>
            <NavbarLogo />
            <div className="flex items-center gap-3">
              <div className="flex h-10 items-center">
                <AnimatedThemeToggler
                  checked={isDark}
                  onToggle={toggleTheme}
                  aria-label={isDark ? "التحول إلى الوضع الفاتح" : "التحول إلى الوضع الداكن"}
                  title={isDark ? "التحول إلى الوضع الفاتح" : "التحول إلى الوضع الداكن"}
                  className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full transition border border-[var(--landing-line)] text-[var(--landing-ink)] hover:bg-[var(--landing-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--landing-accent)]"
                />
              </div>
              <MobileNavToggle
                isOpen={isMobileMenuOpen}
                onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
              />
            </div>
          </MobileNavHeader>

          <MobileNavMenu
            isOpen={isMobileMenuOpen}
            onClose={() => setIsMobileMenuOpen(false)}
          >
            {navLinks.map((link, idx) => (
              <Link
                key={`mobile-link-${idx}`}
                href={link.href}
                onClick={() => setIsMobileMenuOpen(false)}
                className="relative w-full text-right text-lg font-bold text-[var(--landing-ink)]"
              >
                <span className="block">{link.label}</span>
              </Link>
            ))}
            
            <div className="mt-4 flex w-full flex-col gap-4 border-t border-[var(--landing-line)] pt-4">
              {isAuthenticated ? (
                <>
                  <span className="text-right text-sm font-bold text-[var(--landing-muted)]">
                    {user?.fullName}
                  </span>
                  <NavbarButton
                    onClick={() => {
                      handleLogout();
                      setIsMobileMenuOpen(false);
                    }}
                    variant="danger"
                    className="w-full"
                  >
                    خروج
                  </NavbarButton>
                </>
              ) : (
                <>
                  <ShinyButton 
                    href="/login"
                    onClick={() => { setIsMobileMenuOpen(false); }} 
                    className="w-full text-base h-12 flex items-center justify-center"
                  >
                    <LoginNavButtonContent />
                  </ShinyButton>
                  <InteractiveHoverButton 
                    href="/register"
                    onClick={() => { setIsMobileMenuOpen(false); }} 
                    className="w-full text-base h-12 flex items-center justify-center"
                  >
                    احجز مكانك
                  </InteractiveHoverButton>
                </>
              )}
            </div>
          </MobileNavMenu>
        </MobileNav>
      </Navbar>
    </div>
  );
}

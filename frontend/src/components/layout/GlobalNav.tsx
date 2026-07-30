'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useAuthStore } from '@/stores/auth-store';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
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
} from '@/components/ui/resizable-navbar';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { ShinyButton } from '@/components/ui/shiny-button';
import { PlatformLogo } from '@/components/shared/PlatformLogo';
import { InteractiveHoverButton } from '@/components/ui/interactive-hover-button';

function LoginNavButtonContent({ tone }: { tone: 'dark' | 'light' }) {
  return (
    <span className="inline-flex items-center gap-2.5 whitespace-nowrap">
      <span className="inline-flex h-4 w-4 shrink-0">
        <PlatformLogo variant="mark" size="sm" tone={tone} />
      </span>
      <span>تسجيل الدخول</span>
    </span>
  );
}

export function GlobalNav() {
  const pathname = usePathname();
  const user = useAuthStore((state) => state.user);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const logout = useAuthStore((state) => state.logout);
  const router = useRouter();
  const { isDark, toggleTheme } = useAdminTheme();
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

  const isLanding = pathname === '/';
  const isPublicTeachersPage =
    pathname === '/teachers' || pathname.startsWith('/teachers/');
  const isPublicPackagePage = pathname.startsWith('/packages/');
  const isSecondaryResultsPage = pathname.startsWith('/thanaweya-results');
  const isAuthRoute =
    pathname === '/login' ||
    pathname === '/register' ||
    pathname === '/forgot-password';

  if (isAuthRoute) {
    return null;
  }

  const handleLogout = () => {
    void logout().finally(() => {
      router.push('/login');
    });
  };

  const isStaffUser =
    user?.roles?.some(
      (r) =>
        r.toLowerCase().includes('admin') ||
        r.toLowerCase().includes('supervisor') ||
        r.toLowerCase().includes('teacher') ||
        r.toLowerCase().includes('assistant') ||
        r.toLowerCase().includes('staff')
    ) ||
    user?.allowedDomains?.some(
      (d) => d === 'admin' || d === 'teacher' || d === 'assistant'
    );

  const navLinks = isAuthenticated
    ? isStaffUser
      ? [
          { href: '/admin/users', label: 'المستخدمين' },
          { href: '/admin/content', label: 'المحتوى' },
          { href: '/admin/codes', label: 'الأكواد' },
          { href: '/admin/questions', label: 'بنك الأسئلة' },
        ]
      : [
          { href: '/student', label: 'لوحة التحكم' },
          { href: '/student/packages', label: 'باقاتي' },
        ]
    : isLanding ||
        isPublicTeachersPage ||
        isPublicPackagePage ||
        isSecondaryResultsPage
      ? [
          { href: '/', label: 'الرئيسية' },
          { href: '/#courses', label: 'الدورات' },
          { href: '/#teachers', label: 'المعلمون' },
          { href: '/thanaweya-results', label: 'نتيجة الثانوية العامة' },
          { href: '/#about-platform', label: 'عن المنصة' },
          { href: '/#testimonials', label: 'آراء الطلبة' },
          { href: '/parent', label: 'متابعة ولي الأمر' },
        ]
      : [];

  return (
    <div
      className={`z-50 w-full ${isLanding ? 'absolute inset-x-0 top-0' : 'sticky top-0'}`}
    >
      <Navbar isLanding={isLanding}>
        {/* Desktop Navigation */}
        <NavBody isLanding={isLanding}>
          <div className="flex items-center gap-3">
            <NavbarLogo tone={isDark ? 'light' : 'dark'} />
          </div>

          <NavItems
            items={navLinks.map((link) => ({
              name: link.label,
              link: link.href,
            }))}
          />

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
                <ShinyButton
                  href="/login"
                  className="hidden md:inline-flex text-[15px] h-[46px] items-center px-8"
                >
                  <LoginNavButtonContent tone={isDark ? 'light' : 'dark'} />
                </ShinyButton>
                <InteractiveHoverButton
                  href="/register"
                  className="hidden md:inline-flex text-[15px] h-[46px] items-center px-6"
                >
                  إنشاء حساب
                </InteractiveHoverButton>
              </>
            )}

            {/* Theme Toggler right after the main actions */}
            <div className="flex h-10 items-center">
              <AnimatedThemeToggler
                checked={isDark}
                onToggle={toggleTheme}
                aria-label={
                  isDark ? 'التحول إلى الوضع الفاتح' : 'التحول إلى الوضع الداكن'
                }
                title={
                  isDark ? 'التحول إلى الوضع الفاتح' : 'التحول إلى الوضع الداكن'
                }
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full transition border border-[var(--landing-line)] text-[var(--landing-ink)] hover:bg-[var(--landing-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--landing-accent)]"
              />
            </div>
          </div>
        </NavBody>

        {/* Mobile Navigation */}
        <MobileNav>
          <MobileNavHeader>
            <NavbarLogo tone={isDark ? 'light' : 'dark'} />
            <div className="flex items-center gap-1 sm:gap-2">
              {!isAuthenticated && (
                <Link
                  href="/login"
                  aria-label="تسجيل الدخول"
                  className="inline-flex min-h-11 items-center justify-center rounded-full bg-[#0E8F8F] px-3 text-sm font-black text-white transition-colors hover:bg-[#0A7474] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0E8F8F] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--landing-bg)]"
                >
                  دخول
                </Link>
              )}
              <div className="hidden h-10 items-center sm:flex">
                <AnimatedThemeToggler
                  checked={isDark}
                  onToggle={toggleTheme}
                  aria-label={
                    isDark
                      ? 'التحول إلى الوضع الفاتح'
                      : 'التحول إلى الوضع الداكن'
                  }
                  title={
                    isDark
                      ? 'التحول إلى الوضع الفاتح'
                      : 'التحول إلى الوضع الداكن'
                  }
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
                    onClick={() => {
                      setIsMobileMenuOpen(false);
                    }}
                    className="w-full text-base h-12 flex items-center justify-center"
                  >
                    <LoginNavButtonContent tone={isDark ? 'light' : 'dark'} />
                  </ShinyButton>
                  <InteractiveHoverButton
                    href="/register"
                    onClick={() => {
                      setIsMobileMenuOpen(false);
                    }}
                    className="w-full text-base h-12 flex items-center justify-center"
                  >
                    إنشاء حساب
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

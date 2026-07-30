'use client';

import { ReactNode, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  Bell, BookOpen, BookOpenText, Calendar, ChevronDown, ClipboardList, Compass,
  Headphones, Home, LogOut, Menu, MessageSquareText, PanelRightClose,
  PanelRightOpen, PhoneCall, Search, Shield, Star, X, type LucideIcon,
  WalletCards, Users, BarChart3,
} from 'lucide-react';

import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { useAuthStore } from '@/stores/auth-store';
import { useHasPermission } from '@/hooks/useHasPermission';

export type AssistantShellRoute =
  | '/assistant/dashboard' | '/assistant/tasks' | '/assistant/crm' | '/assistant/chat'
  | '/assistant/live-support' | '/assistant/attendance' | '/assistant/vacations'
  | '/assistant/notifications' | '/assistant/content' | '/assistant/community'
  | '/assistant/questions' | '/assistant/watch-requests' | '/assistant/payroll'
  | '/assistant/financial-requests' | '/assistant/recharge-verification' | '/assistant/students' | '/assistant/reports';

type AssistantShellChromeProps = {
  activePath: AssistantShellRoute;
  sectionLabel: string;
  pageTitle: string;
  subtitle?: string;
  action?: ReactNode;
  headerAccessory?: ReactNode;
  children: ReactNode;
};

type AssistantNavItem = { href: AssistantShellRoute; label: string; icon: LucideIcon; group: 'operations' | 'learning' | 'communication' | 'employee'; permission?: string };

const navItems: AssistantNavItem[] = [
  { href: '/assistant/tasks', label: 'المهام والعمليات', icon: ClipboardList, group: 'operations', permission: 'tasks.manage' },
  { href: '/assistant/content', label: 'إدارة المحتوى التعليمي', icon: BookOpen, group: 'learning', permission: 'content.manage' },
  { href: '/assistant/community', label: 'إدارة مجتمع الطلاب', icon: MessageSquareText, group: 'learning', permission: 'community.manage' },
  { href: '/assistant/questions', label: 'الامتحانات والأسئلة', icon: Shield, group: 'learning', permission: 'exams.manage' },
  { href: '/assistant/watch-requests', label: 'طلبات إعادة المشاهدة', icon: Star, group: 'learning', permission: 'watch_requests.manage' },
  { href: '/assistant/crm', label: 'الكول سنتر', icon: PhoneCall, group: 'communication', permission: 'crm.manage' },
  { href: '/assistant/live-support', label: 'الدعم المباشر', icon: Headphones, group: 'communication' },
  { href: '/assistant/recharge-verification', label: 'مطابقة الشحن', icon: WalletCards, group: 'operations', permission: 'payments.manage' },
  { href: '/assistant/students', label: 'إدارة الطلاب', icon: Users, group: 'operations', permission: 'users.manage' },
  { href: '/assistant/reports', label: 'مركز التقارير', icon: BarChart3, group: 'operations', permission: 'reports.manage' },
  { href: '/assistant/chat', label: 'التواصل الداخلي', icon: MessageSquareText, group: 'communication', permission: 'chat.manage' },
  { href: '/assistant/attendance', label: 'الحضور والانصراف', icon: Calendar, group: 'employee' },
  { href: '/assistant/vacations', label: 'طلبات الإجازة', icon: Compass, group: 'employee' },
  { href: '/assistant/payroll', label: 'راتبي', icon: ClipboardList, group: 'employee' },
  { href: '/assistant/financial-requests', label: 'الطلبات المالية', icon: ClipboardList, group: 'employee' },
  { href: '/assistant/notifications', label: 'الإشعارات', icon: Bell, group: 'employee' },
];

const GROUPS: Array<{ id: AssistantNavItem['group']; label: string; icon: LucideIcon }> = [
  { id: 'operations', label: 'المهام والعمليات', icon: ClipboardList },
  { id: 'learning', label: 'التعليم والمحتوى', icon: BookOpenText },
  { id: 'communication', label: 'التواصل وخدمة الطلاب', icon: Headphones },
  { id: 'employee', label: 'شؤون الموظف', icon: Calendar },
];

export function AssistantShellChrome({ activePath, sectionLabel, pageTitle, subtitle, action, headerAccessory, children }: AssistantShellChromeProps) {
  const router = useRouter();
  const logout = useAuthStore((state) => state.logout);
  const user = useAuthStore((state) => state.user);
  const { hasPermission } = useHasPermission();
  const { isDark, themeVars, toggleTheme } = useAdminTheme();
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);
  const [navQuery, setNavQuery] = useState('');
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>(() => {
    const active = navItems.find((item) => item.href === activePath);
    return active ? { [active.group]: true } : {};
  });

  useRootOverscrollBackground();

  const filteredNavItems = useMemo(() => {
    let items = navItems.filter((item) => !item.permission || hasPermission(item.permission));
    const allowed = user?.allowedNavbarItems;
    if (allowed?.length) items = items.filter((item) => allowed.some((path) => path === item.href || path.startsWith(`${item.href}/`)));
    return items;
  }, [hasPermission, user?.allowedNavbarItems]);

  const normalizedQuery = navQuery.trim().toLocaleLowerCase('ar');
  const navGroups = GROUPS.map((group) => ({
    ...group,
    items: filteredNavItems.filter((item) => item.group === group.id && (!normalizedQuery || group.label.toLocaleLowerCase('ar').includes(normalizedQuery) || item.label.toLocaleLowerCase('ar').includes(normalizedQuery))),
  })).filter((group) => group.items.length > 0);

  const toggleGroup = (id: string) => setExpandedGroups((current) => ({ ...current, [id]: !current[id] }));
  const handleLogout = () => void logout().finally(() => router.replace('/login'));
  const isDashboardActive = activePath === '/assistant/dashboard';
  const mobileQuickItems = filteredNavItems.slice(0, 3);
  const mobileMoreItems = filteredNavItems.slice(3);

  const renderGroups = (mobile = false) => navGroups.map((group) => {
    const GroupIcon = group.icon;
    const isActive = group.items.some((item) => item.href === activePath);
    const isExpanded = mobile || normalizedQuery.length > 0 || Boolean(expandedGroups[group.id]);
    return <div key={group.id} className="space-y-1">
      <button type="button" onClick={() => toggleGroup(group.id)} className={`flex h-11 w-full items-center gap-3 rounded-xl px-3 text-right transition-colors ${isActive ? 'bg-[var(--admin-primary-15)] font-bold text-[var(--admin-primary)]' : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'} ${isSidebarCollapsed && !mobile ? 'justify-center' : 'justify-between'}`} title={group.label}>
        <span className="flex items-center gap-3"><GroupIcon className="h-5 w-5 shrink-0" />{(!isSidebarCollapsed || mobile) && <span className="text-sm font-bold">{group.label}</span>}</span>
        {(!isSidebarCollapsed || mobile) && <ChevronDown className={`h-4 w-4 transition-transform ${isExpanded ? 'rotate-180' : ''}`} />}
      </button>
      {isExpanded && (!isSidebarCollapsed || mobile) && <div className="mt-1 space-y-1 pr-4">
        {group.items.map((item) => {
          const Icon = item.icon;
          const isItemActive = item.href === activePath;
          return <Link key={item.href} href={item.href} prefetch={false} onClick={() => mobile && setIsMobileMenuOpen(false)} aria-current={isItemActive ? 'page' : undefined} className={`flex h-10 items-center gap-3 rounded-lg px-3 text-right transition-colors ${isItemActive ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'} `}>
            <Icon className="h-4.5 w-4.5 shrink-0" /><span className="truncate text-xs font-bold">{item.label}</span>
          </Link>;
        })}
      </div>}
    </div>;
  });

  return <div dir="rtl" className="relative h-dvh max-h-dvh overflow-x-hidden bg-[var(--admin-bg)] text-[var(--admin-text)]" style={themeVars}>
    <aside className={`fixed right-0 top-0 z-50 hidden h-full flex-col justify-between border-l border-[var(--admin-border)] bg-[var(--admin-sidebar)] py-5 transition-[width] duration-200 ease-out lg:flex ${isSidebarCollapsed ? 'w-20' : 'w-72'}`} role="navigation" aria-label="قائمة الموظف الرئيسية">
      <div className="flex min-h-0 flex-1 flex-col">
        <div className={`mb-5 flex items-center ${isSidebarCollapsed ? 'justify-center px-3' : 'justify-between px-5'}`}>
          <div className="flex min-w-0 items-center gap-3"><div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]"><BookOpenText className="h-5 w-5" /></div>{!isSidebarCollapsed && <span className="truncate text-sm font-bold">مساحة الموظفين</span>}</div>
          <button type="button" onClick={() => setIsSidebarCollapsed(!isSidebarCollapsed)} className={`${isSidebarCollapsed ? 'absolute left-[-1.25rem] top-5 rounded-l-xl border border-[var(--admin-border)] bg-[var(--admin-sidebar)]' : ''} flex h-10 w-10 items-center justify-center rounded-xl text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]`} aria-label={isSidebarCollapsed ? 'توسيع القائمة الجانبية' : 'طي القائمة الجانبية'}>{isSidebarCollapsed ? <PanelRightOpen className="h-5 w-5" /> : <PanelRightClose className="h-5 w-5" />}</button>
        </div>
        {!isSidebarCollapsed && <label className="relative mx-4 mb-4 block"><Search className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" /><input value={navQuery} onChange={(event) => setNavQuery(event.target.value)} className="h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] py-2 pr-10 pl-3 text-sm font-medium outline-none placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary-15)]" placeholder="ابحث عن صفحة أو أداة" aria-label="ابحث في صفحات الموظف" /></label>}
        <nav className={`min-h-0 flex-1 space-y-2 overflow-y-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden ${isSidebarCollapsed ? 'px-3' : 'px-4'}`}><Link href="/assistant/dashboard" prefetch={false} aria-current={isDashboardActive ? 'page' : undefined} className={`flex h-11 items-center gap-3 rounded-xl transition-colors ${isSidebarCollapsed ? 'justify-center px-3' : 'px-3'} ${isDashboardActive ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'}`}><Home className="h-5 w-5 shrink-0" />{!isSidebarCollapsed && <span className="text-sm font-bold">الرئيسية</span>}</Link>{renderGroups()}</nav>
      </div>
      <div className={`mt-4 space-y-2 ${isSidebarCollapsed ? 'px-3' : 'px-4'}`}>
        <div className={`flex items-center px-1 ${isSidebarCollapsed ? 'justify-center' : 'justify-start'}`}><AnimatedThemeToggler checked={isDark} onToggle={toggleTheme} aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'} title={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'} className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]" />{!isSidebarCollapsed && <span className="mr-3 text-sm font-bold text-[var(--admin-muted)]">{isDark ? 'الوضع الفاتح' : 'الوضع الداكن'}</span>}</div>
        <button type="button" onClick={handleLogout} className={`flex h-11 w-full items-center gap-3 rounded-xl text-[var(--admin-danger)] transition-colors hover:bg-[var(--admin-hover)] ${isSidebarCollapsed ? 'justify-center px-3' : 'px-3'}`}><LogOut className="h-5 w-5 shrink-0" />{!isSidebarCollapsed && <span className="text-sm font-bold">تسجيل الخروج</span>}</button>
      </div>
    </aside>
    <main className={`app-shell-scroll relative z-10 h-dvh overflow-y-auto overscroll-y-auto px-4 py-6 pb-[calc(8rem+env(safe-area-inset-bottom))] transition-[margin] duration-200 lg:px-7 lg:py-8 lg:pb-10 ${isSidebarCollapsed ? 'lg:mr-20' : 'lg:mr-72'}`}>
      <header className="mb-8 flex w-full flex-col gap-4 md:flex-row md:items-end md:justify-between lg:mb-9"><div className="w-full"><div className="mb-4 flex items-center justify-end gap-2 lg:hidden"><AnimatedThemeToggler checked={isDark} onToggle={toggleTheme} aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'} className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]" /><button type="button" onClick={handleLogout} className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-danger)] hover:bg-[var(--admin-hover)]" aria-label="تسجيل الخروج"><LogOut className="h-4 w-4" /></button></div><div className="flex flex-wrap items-center gap-3"><div><p className="mb-1 text-xs font-black tracking-[0.22em] text-[var(--admin-primary)]">{sectionLabel}</p><h1 className="mb-1 text-3xl font-extrabold tracking-tight lg:text-4xl">{pageTitle}</h1>{subtitle && <p className="max-w-3xl text-sm font-medium leading-6 text-[var(--admin-muted)]">{subtitle}</p>}</div>{headerAccessory}</div></div>{action}</header>
      {children}<footer className="mt-14 flex select-none flex-col items-center opacity-60"><div className="mb-4 h-px w-full bg-[var(--admin-border)]" /><p className="text-xs font-bold text-[var(--admin-muted)]">منصة مسار</p></footer>
    </main>
    <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-[var(--admin-border)] bg-[var(--admin-sidebar)] px-3 py-3 lg:hidden"><div className="mx-auto grid w-full max-w-md grid-cols-5 gap-2"><Link href="/assistant/dashboard" className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] p-2 text-xs font-black ${isDashboardActive ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'}`}><Home className="h-5 w-5" /><span>الرئيسية</span></Link>{mobileQuickItems.map((item) => { const Icon = item.icon; const active = item.href === activePath; return <Link key={item.href} href={item.href} className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] p-2 text-center text-xs font-black ${active ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'}`}><Icon className="h-5 w-5" /><span className="w-full truncate">{item.label}</span></Link>; })}{mobileMoreItems.length > 0 && <button type="button" onClick={() => setIsMobileMenuOpen(true)} className="flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-2 text-xs font-black text-[var(--admin-muted)]"><Menu className="h-5 w-5" /><span>المزيد</span></button>}</div></nav>
    {isMobileMenuOpen && <div className="fixed inset-0 z-50 flex lg:hidden" role="dialog" aria-modal="true"><button type="button" className="fixed inset-0 bg-black/40" aria-label="إغلاق القائمة" onClick={() => setIsMobileMenuOpen(false)} /><aside className="relative mr-auto flex w-72 max-w-[86vw] flex-col bg-[var(--admin-sidebar)] px-4 py-5 shadow-[12px_0_40px_var(--admin-shadow)]"><div className="mb-5 flex items-center justify-between"><span className="text-sm font-bold">مساحة الموظفين</span><button type="button" onClick={() => setIsMobileMenuOpen(false)} className="rounded-lg p-2 hover:bg-[var(--admin-hover)]" aria-label="إغلاق القائمة"><X className="h-5 w-5" /></button></div><nav className="min-h-0 flex-1 overflow-y-auto"><Link href="/assistant/dashboard" onClick={() => setIsMobileMenuOpen(false)} className={`mb-2 flex h-11 items-center gap-3 rounded-xl px-3 text-sm font-bold ${isDashboardActive ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'text-[var(--admin-muted)]'}`}><Home className="h-5 w-5" />الرئيسية</Link>{renderGroups(true)}</nav><div className="mt-4 flex items-center justify-between border-t border-[var(--admin-border)] pt-4"><AnimatedThemeToggler checked={isDark} onToggle={toggleTheme} /><button type="button" onClick={handleLogout} className="flex h-10 items-center gap-2 rounded-lg px-2 text-sm font-bold text-[var(--admin-danger)]"><LogOut className="h-5 w-5" />تسجيل الخروج</button></div></aside></div>}
  </div>;
}

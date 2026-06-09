import {
  ArrowLeft,
  BookOpen,
  BookOpenText,
  KeyRound,
  Layers3,
  MessageSquareText,
  Shield,
  Sparkles,
  UserCog,
  Users,
  Wrench,
  GraduationCap,
  Library,
  Coins,
  type LucideIcon,
} from 'lucide-react';

export interface AdminRootLink {
  href: string;
  title: string;
  body: string;
  icon: LucideIcon;
}

export const adminMenuItems = [
  { label: 'المستندات والمواد', href: '/admin/subjects', icon: <Library className="h-4 w-4" />, permission: 'content.manage' },
  { label: 'المعلمين', href: '/admin/teachers', icon: <GraduationCap className="h-4 w-4" />, permission: 'users.manage' },
  { label: 'المستخدمين', href: '/admin/users', icon: <Users className="h-4 w-4" />, permission: 'users.manage' },
  { label: 'المحتوى', href: '/admin/content', icon: <BookOpen className="h-4 w-4" />, permission: 'content.manage' },
  { label: 'المجتمع', href: '/admin/community', icon: <MessageSquareText className="h-4 w-4" />, permission: 'community.manage' },
  { label: 'تحليل AI', href: '/admin/ai-monitor', icon: <Sparkles className="h-4 w-4" />, permission: 'reports.manage' },
  { label: 'أكواد الوصول', href: '/admin/codes', icon: <KeyRound className="h-4 w-4" />, permission: 'codes.manage' },
  { label: 'بنك الأسئلة', href: '/admin/questions', icon: <Shield className="h-4 w-4" />, permission: 'exams.manage' },
  { label: 'التعديلات', href: '/admin/overrides', icon: <Wrench className="h-4 w-4" />, permission: 'users.manage' },
  { label: 'المالية والرواتب', href: '/admin/finance', icon: <Coins className="h-4 w-4" />, permission: 'users.manage' },
  { label: 'التواصل الداخلي', href: '/admin/chat', icon: <MessageSquareText className="h-4 w-4" /> },
];

export const adminRootLinks: AdminRootLink[] = [
  {
    href: '/admin/subjects',
    title: 'المواد الدراسية',
    body: 'إدارة المواد والوصف التوضيحي لكل مادة.',
    icon: Library,
  },
  {
    href: '/admin/teachers',
    title: 'إدارة المعلمين',
    body: 'إدارة ملفات المعلمين وتعيين المواد والنسب المئوية ونقاط الاتصال.',
    icon: GraduationCap,
  },
  {
    href: '/admin/users',
    title: 'إدارة المستخدمين',
    body: 'الحسابات والصلاحيات والحالات وأجهزة الدخول.',
    icon: UserCog,
  },
  {
    href: '/admin/content',
    title: 'إدارة المحتوى',
    body: 'الباقات والأقسام والدروس والفيديوهات.',
    icon: BookOpenText,
  },
  {
    href: '/admin/codes',
    title: 'أكواد الوصول',
    body: 'توليد الأكواد وتتبع استخدامها وتصديرها.',
    icon: KeyRound,
  },
  {
    href: '/admin/questions',
    title: 'بنك الأسئلة',
    body: 'الأسئلة والاختيارات والنقاط والتصنيفات.',
    icon: Shield,
  },
  {
    href: '/admin/overrides',
    title: 'التعديلات اليدوية',
    body: 'فتح الدروس وتصفير المشاهدات للحالات الخاصة.',
    icon: Wrench,
  },
  {
    href: '/admin/finance',
    title: 'المالية والحسابات',
    body: 'إدارة رواتب الموظفين والزيادات والخصومات، ومراجعة أرباح المعلمين وتسوية سحوباتهم.',
    icon: Coins,
  },
];

export const adminRootStats = {
  structure: Layers3,
  ready: Sparkles,
  route: ArrowLeft,
};

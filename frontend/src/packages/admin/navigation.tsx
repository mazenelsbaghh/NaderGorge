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
  type LucideIcon,
} from 'lucide-react';

export interface AdminRootLink {
  href: string;
  title: string;
  body: string;
  icon: LucideIcon;
}

export const adminMenuItems = [
  { label: 'المستخدمين', href: '/admin/users', icon: <Users className="h-4 w-4" /> },
  { label: 'المحتوى', href: '/admin/content', icon: <BookOpen className="h-4 w-4" /> },
  { label: 'المجتمع', href: '/admin/community', icon: <MessageSquareText className="h-4 w-4" /> },
  { label: 'تحليل AI', href: '/admin/ai-monitor', icon: <Sparkles className="h-4 w-4" /> },
  { label: 'أكواد الوصول', href: '/admin/codes', icon: <KeyRound className="h-4 w-4" /> },
  { label: 'بنك الأسئلة', href: '/admin/questions', icon: <Shield className="h-4 w-4" /> },
  { label: 'التعديلات', href: '/admin/overrides', icon: <Wrench className="h-4 w-4" /> },
];

export const adminRootLinks: AdminRootLink[] = [
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
];

export const adminRootStats = {
  structure: Layers3,
  ready: Sparkles,
  route: ArrowLeft,
};

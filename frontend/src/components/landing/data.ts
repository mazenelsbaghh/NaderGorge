import {
  BookOpenCheck,
  CalendarCheck,
  Clock3,
  GraduationCap,
  Headphones,
  NotebookPen,
  ShieldCheck,
  Sparkles,
  Trophy,
  UsersRound,
} from "lucide-react";

export const navigationLinks = [
  { href: "/", label: "الرئيسية" },
  { href: "#courses", label: "الدورات" },
  { href: "#teachers", label: "المعلمون" },
  { href: "#about-platform", label: "عن المنصة" },
  { href: "#contact", label: "تواصل معنا" },
] as const;

export const heroHighlights = [
  { label: "دروس تفاعلية", icon: BookOpenCheck },
  { label: "خطة يومية", icon: NotebookPen },
  { label: "متابعة مستمرة", icon: CalendarCheck },
  { label: "مراجعات منظمة", icon: ShieldCheck },
] as const;

export const platformStats = [
  { value: "+150", label: "معلم مميز", icon: GraduationCap },
  { value: "+50", label: "مادة تعليمية", icon: BookOpenCheck },
  { value: "+10K", label: "طالب مستفيد", icon: UsersRound },
  { value: "+95%", label: "نسبة رضا الطلاب", icon: Sparkles },
] as const;

export const topStudents = [
  {
    rank: 1,
    name: "أحمد خالد",
    stage: "القسم العلمي",
    score: "98.7%",
    avatar: "https://avatar.vercel.sh/ahmed-khaled",
  },
  {
    rank: 2,
    name: "سارة محمد",
    stage: "القسم العلمي",
    score: "98.1%",
    avatar: "https://avatar.vercel.sh/sara-mohamed",
  },
  {
    rank: 3,
    name: "محمد إياد",
    stage: "القسم الأدبي",
    score: "97.6%",
    avatar: "https://avatar.vercel.sh/mohamed-eyad",
  },
  {
    rank: 4,
    name: "نور الهدى",
    stage: "القسم الأدبي",
    score: "97.2%",
    avatar: "https://avatar.vercel.sh/nour-alhoda",
  },
  {
    rank: 5,
    name: "علي حسن",
    stage: "القسم العلمي",
    score: "96.8%",
    avatar: "https://avatar.vercel.sh/ali-hassan",
  },
] as const;

export const teachers = [
  {
    name: "أ. خالد النجار",
    subject: "معلم الرياضيات",
    rating: "4.9",
    avatar: "https://avatar.vercel.sh/khaled-teacher",
  },
  {
    name: "أ. إيمان سعيد",
    subject: "معلمة الفيزياء",
    rating: "4.9",
    avatar: "https://avatar.vercel.sh/eman-teacher",
  },
  {
    name: "أ. محمد عصام",
    subject: "معلم الكيمياء",
    rating: "4.8",
    avatar: "https://avatar.vercel.sh/mohamed-teacher",
  },
  {
    name: "أ. عمر حمدي",
    subject: "معلم اللغة العربية",
    rating: "4.9",
    avatar: "https://avatar.vercel.sh/omar-teacher",
  },
] as const;

export const testimonials = [
  {
    name: "يوسف م.",
    avatar: "https://avatar.vercel.sh/youssef",
    quote: "منصة التعلم الشرح مبسط والدروس بتنتهي بسرعة، أرفع مستواي بشكل كبير.",
  },
  {
    name: "لينا ع.",
    avatar: "https://avatar.vercel.sh/lina",
    quote: "أفضل منصة تعليمية استخدمتها، التقدم سريع والمحتوى ممتاز جدًا.",
  },
  {
    name: "أحمد ر.",
    avatar: "https://avatar.vercel.sh/ahmed-review",
    quote: "جزاكم الله خير، بفضل الله ثم بفضلكم حققت حلمي في الثانوية العامة.",
  },
] as const;

export const educationTracks = [
  {
    title: "البكالوريا",
    description: "مناهج شاملة ومبسطة لجميع مواد البكالوريا، مع اختبارات وتمارين على نمط الامتحان.",
    icon: GraduationCap,
    cta: "استكشف مسار البكالوريا",
    href: "/register",
    tone: "teal",
  },
  {
    title: "الثانوية العامة",
    description: "جميع مواد الثانوية العامة: شرح، ملخصات، اختبارات دورية، ومراجعة منظمة قبل الامتحان.",
    icon: BookOpenCheck,
    cta: "استكشف مسار الثانوية العامة",
    href: "/register",
    tone: "navy",
  },
] as const;

export const finalCtaFeatures = [
  { label: "محتوى شامل", detail: "شرح مبسط ومحدث", icon: Sparkles },
  { label: "دعم فني وتعليمي", detail: "على مدار الساعة", icon: Headphones },
  { label: "متابعة وتقييم", detail: "لتطوير مستواك", icon: Trophy },
  { label: "تعلم في وقتك", detail: "ومن أي مكان", icon: Clock3 },
] as const;

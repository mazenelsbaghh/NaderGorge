import {
  BookOpenText,
  GraduationCap,
  LibraryBig,
  Map,
  ScrollText,
  Sparkles,
  Target,
  Trophy,
} from "lucide-react";

export const navigationLinks = [
  { href: "#features", label: "مميزات التجربة" },
  { href: "#subjects", label: "فروع الدراسة" },
  { href: "#testimonials", label: "آراء الطلبة" },
];

export const heroStats = [
  { value: "+1200", label: "طالب بدأ رحلته" },
  { value: "4", label: "مسارات تعليمية" },
  { value: "24/7", label: "وصول من أي جهاز" },
];

export const features = [
  {
    icon: ScrollText,
    title: "شرح بصري حي",
    description:
      "دروس مبنية كرحلة داخل الحضارة: شرح منظم، أمثلة واضحة، وربط مباشر بين الفكرة والسؤال.",
  },
  {
    icon: Target,
    title: "تدريب يثبت الفهم",
    description:
      "اختبارات قصيرة بعد كل جزء علشان تقيس الاستيعاب بسرعة وتعرف محتاج تراجع إيه.",
  },
  {
    icon: Sparkles,
    title: "هوية مصرية معاصرة",
    description:
      "تصميم فرعوني حديث يدي انطباع قوي ويخلّي المنصة مميزة ومتماسكة بصريًا.",
  },
  {
    icon: GraduationCap,
    title: "توجيه واضح للطالب",
    description:
      "المحتوى مترتب خطوة بخطوة علشان الطالب يعرف يبدأ منين ويتقدم إزاي بدون تشتيت.",
  },
];

export const subjects = [
  {
    title: "التاريخ",
    description: "سرد ممتع للأحداث مع خرائط وتسلسل زمني يسهّل الحفظ والفهم.",
    icon: LibraryBig,
  },
  {
    title: "الجغرافيا",
    description: "شرح مرئي للخرائط والظواهر مع ربط مباشر بالأسئلة الوزارية.",
    icon: Map,
  },
  {
    title: "المهارات البحثية",
    description: "تدريب على التحليل، تنظيم المعلومات، وصياغة إجابات قوية بثقة.",
    icon: BookOpenText,
  },
  {
    title: "المراجعات النهائية",
    description: "محطات مراجعة مركزة قبل الامتحان تركّز على الأفكار المتكررة والنقاط الفارقة.",
    icon: Trophy,
  },
];

export const testimonials = [
  {
    name: "سلمى أحمد",
    role: "طالبة بالصف الثالث الثانوي",
    quote:
      "الشرح مرتب جدًا، وحسيت إن كل درس بيكمل اللي قبله. الجو العام مميز وخلاني أحب أفتح المنصة.",
  },
  {
    name: "يوسف خالد",
    role: "طالب بالصف الثاني الثانوي",
    quote:
      "الاختبارات بعد الشرح فرقت معايا، وبقيت أعرف مستوايا بسرعة بدل ما أذاكر من غير اتجاه واضح.",
  },
  {
    name: "ملك طارق",
    role: "ولية أمر",
    quote:
      "المنصة شكلها احترافي ومريح، والأهم إن المحتوى واضح وسهل المتابعة للطالب في البيت.",
  },
];


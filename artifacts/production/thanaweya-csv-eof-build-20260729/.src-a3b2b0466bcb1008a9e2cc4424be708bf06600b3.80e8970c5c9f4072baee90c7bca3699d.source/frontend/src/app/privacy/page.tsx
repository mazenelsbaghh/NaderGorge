import Link from 'next/link';
import { ArrowUpLeft, ShieldCheck } from 'lucide-react';

const sections = [
  {
    title: 'البيانات التي نجمعها',
    body: [
      'بيانات الحساب الأساسية مثل الاسم، رقم الهاتف، المرحلة الدراسية، الصف، المدرسة، وكود متابعة ولي الأمر.',
      'بيانات الاستخدام التعليمية مثل الدروس التي تمت مشاهدتها، نتائج الامتحانات، تسليمات الواجبات، الحضور، الرصيد، والتنبيهات المرتبطة بالطالب.',
      'بيانات الجهاز اللازمة لتشغيل الخدمة مثل رمز إشعارات Firebase Cloud Messaging، نوع الجهاز، ونظام التشغيل عند الحاجة لتأمين الحساب وإرسال التنبيهات.',
    ],
  },
  {
    title: 'كيف نستخدم البيانات',
    body: [
      'تقديم خدمات منصة مسار التعليمية للطالب وولي الأمر.',
      'عرض تقارير المتابعة لولي الأمر، مثل الحضور، المشاهدات، الدرجات، الواجبات، والتنبيهات.',
      'إرسال إشعارات مهمة عن نشاط الطالب أو تحديثات التطبيق أو الإجراءات المطلوبة.',
      'حماية الحسابات، منع إساءة الاستخدام، وتحسين جودة الخدمة والدعم الفني.',
    ],
  },
  {
    title: 'مشاركة البيانات',
    body: [
      'لا نبيع بيانات الطلاب أو أولياء الأمور.',
      'قد نشارك بيانات محدودة مع مزودي خدمات ضروريين لتشغيل المنصة، مثل الاستضافة، قواعد البيانات، Firebase Cloud Messaging، وخدمات التخزين أو التحليلات التشغيلية.',
      'قد نكشف البيانات إذا كان ذلك مطلوبًا قانونيًا أو لحماية حقوق المنصة والمستخدمين.',
    ],
  },
  {
    title: 'الأمان والاحتفاظ بالبيانات',
    body: [
      'نستخدم ضوابط فنية وتنظيمية لتقليل مخاطر الوصول غير المصرح به إلى البيانات.',
      'نحتفظ بالبيانات طالما كانت لازمة لتقديم الخدمة، المتابعة التعليمية، الالتزامات القانونية، أو معالجة طلبات الدعم.',
      'يمكن تعطيل أو حذف بعض البيانات بناءً على طلب صاحب الحساب عندما يسمح النظام والالتزامات التشغيلية بذلك.',
    ],
  },
  {
    title: 'حقوق المستخدم والتواصل',
    body: [
      'يمكنك طلب مراجعة بياناتك أو تعديلها أو الاستفسار عن استخدامها من خلال قنوات الدعم الرسمية للمنصة.',
      'إذا كان لديك سؤال بخصوص الخصوصية أو تطبيق ولي الأمر، تواصل معنا عبر الدعم الفني المتاح داخل المنصة.',
    ],
  },
];

export const metadata = {
  title: 'سياسة الخصوصية | منصة مسار',
  description: 'سياسة الخصوصية لتطبيق ولي الأمر ومنصة مسار التعليمية.',
};

export default function PrivacyPolicyPage() {
  return (
    <main className="landing-page min-h-screen">
      <div className="landing-page__backdrop" />
      <div className="landing-page__texture" />

      <section className="relative z-10 mx-auto max-w-4xl px-6 py-24 text-right">
        <div className="mb-10">
          <div className="landing-chip mb-5">
            <ShieldCheck className="h-4 w-4" />
            <span>سياسة الخصوصية</span>
          </div>
          <h1 className="text-4xl font-black leading-tight text-[var(--landing-ink)] md:text-5xl">
            سياسة الخصوصية لتطبيق ولي الأمر
          </h1>
          <p className="mt-4 text-base font-semibold leading-8 text-[var(--landing-muted)]">
            توضح هذه السياسة كيفية جمع واستخدام وحماية البيانات داخل منصة مسار
            وتطبيق ولي الأمر. آخر تحديث: 6 يوليو 2026.
          </p>
        </div>

        <div className="space-y-5">
          {sections.map((section) => (
            <article key={section.title} className="landing-panel rounded-[24px] p-6">
              <h2 className="text-xl font-black text-[var(--landing-ink)]">
                {section.title}
              </h2>
              <ul className="mt-4 space-y-3 text-sm font-semibold leading-7 text-[var(--landing-muted)]">
                {section.body.map((item) => (
                  <li key={item} className="flex gap-3">
                    <span className="mt-2 h-2 w-2 shrink-0 rounded-full bg-[var(--landing-accent)]" />
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
            </article>
          ))}
        </div>

        <div className="mt-10 flex justify-start">
          <Link
            href="/"
            className="inline-flex items-center gap-2 rounded-full bg-[var(--landing-accent)] px-6 py-3 text-sm font-extrabold text-[var(--landing-accent-foreground)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-accent-strong)]"
          >
            العودة للرئيسية
            <ArrowUpLeft className="h-4 w-4" />
          </Link>
        </div>
      </section>
    </main>
  );
}

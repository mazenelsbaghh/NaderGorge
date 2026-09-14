import Link from 'next/link';
import { ArrowUpLeft, Mail, ShieldCheck, Trash2 } from 'lucide-react';

const supportEmail = 'mazenelsbagh1@gmail.com';

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
    title: 'حذف الحساب والبيانات',
    body: [
      'يحق للمستخدم أو ولي الأمر طلب حذف البيانات المرتبطة به في أي وقت من خلال صفحة طلب حذف الحساب والبيانات الموضحة أدناه.',
      `يمكن إرسال الطلب من البريد المسجل أو بريد ولي الأمر إلى ${supportEmail} بعنوان «طلب حذف بيانات تطبيق ولي الأمر»، مع كتابة رقم الهاتف المرتبط بالحساب أو كود متابعة ولي الأمر للتحقق من ملكية الطلب.`,
      'نراجع الطلب ونتحقق من هوية صاحبه، ثم نحذف أو نخفي هوية البيانات غير المطلوب الاحتفاظ بها خلال مدة لا تتجاوز 30 يومًا.',
      'قد نحتفظ فقط بالسجلات التي يفرضها القانون أو اللازمة لمنع الاحتيال وتسوية المعاملات والنزاعات، ثم نحذفها أو نخفي هويتها بعد انتهاء مدة الاحتفاظ النظامية.',
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
  title: 'سياسة الخصوصية | متابعه ولي أمر- مسار اكاديمي',
  description:
    'سياسة الخصوصية لتطبيق متابعه ولي أمر- مسار اكاديمي، وشرح طلب حذف الحساب والبيانات.',
};

function AppIdentityCard() {
  const identityFields = [
    ['اسم التطبيق على Google Play', 'متابعه ولي أمر- مسار اكاديمي', 'rtl'],
    ['اسم المطوّر', 'EGY Legal for smart technology', 'ltr'],
    ['معرّف التطبيق', 'com.massar.parent', 'ltr'],
    ['مسؤول التواصل', 'mazen nasser', 'ltr'],
  ] as const;

  return (
    <article className="landing-panel mb-5 rounded-2xl p-6">
      <h2 className="text-xl font-black text-[var(--landing-ink)]">
        بيانات التطبيق والمطوّر
      </h2>
      <dl className="mt-4 grid gap-4 text-sm font-semibold leading-7 text-[var(--landing-muted)] sm:grid-cols-2">
        {identityFields.map(([label, text, direction]) => (
          <div key={label}>
            <dt className="font-black text-[var(--landing-ink)]">{label}</dt>
            <dd dir={direction} className="text-right">
              {text}
            </dd>
          </div>
        ))}
      </dl>
    </article>
  );
}

function PrivacyActions() {
  return (
    <div className="mt-10 flex flex-wrap justify-start gap-3">
      <Link
        href="/account-deletion"
        className="inline-flex items-center gap-2 rounded-full border border-[var(--landing-accent)] px-6 py-3 text-sm font-extrabold text-[var(--landing-accent)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-teal-soft)]"
      >
        طلب حذف الحساب والبيانات
        <Trash2 className="h-4 w-4" />
      </Link>
      <a
        href={`mailto:${supportEmail}?subject=${encodeURIComponent('طلب حذف بيانات تطبيق ولي الأمر')}`}
        className="inline-flex items-center gap-2 rounded-full border border-[var(--landing-accent)] px-6 py-3 text-sm font-extrabold text-[var(--landing-accent)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-teal-soft)]"
      >
        مراسلة دعم الخصوصية
        <Mail className="h-4 w-4" />
      </a>
      <Link
        href="/"
        className="inline-flex items-center gap-2 rounded-full bg-[var(--landing-accent)] px-6 py-3 text-sm font-extrabold text-[var(--landing-accent-foreground)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-accent-strong)]"
      >
        العودة للرئيسية
        <ArrowUpLeft className="h-4 w-4" />
      </Link>
    </div>
  );
}

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
            سياسة الخصوصية لتطبيق متابعه ولي أمر- مسار اكاديمي
          </h1>
          <p className="mt-4 text-base font-semibold leading-8 text-[var(--landing-muted)]">
            توضح هذه السياسة كيفية جمع واستخدام وحماية البيانات داخل التطبيق
            ومنصة مسار. آخر تحديث: 17 أغسطس 2026.
          </p>
        </div>

        <AppIdentityCard />

        <div className="space-y-5">
          {sections.map((section) => (
            <article
              key={section.title}
              className="landing-panel rounded-2xl p-6"
            >
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

        <PrivacyActions />
      </section>
    </main>
  );
}

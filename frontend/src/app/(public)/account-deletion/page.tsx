import Link from 'next/link';
import { ArrowUpLeft, Mail, ShieldCheck, Trash2 } from 'lucide-react';

const supportEmail = 'mazenelsbagh1@gmail.com';
const deletionRequestSubject = 'طلب حذف بيانات تطبيق ولي الأمر';

export const metadata = {
  title: 'طلب حذف الحساب والبيانات | متابعه ولي أمر- مسار اكاديمي',
  description:
    'خطوات طلب حذف الحساب والبيانات من تطبيق متابعه ولي أمر- مسار اكاديمي.',
};

const deletionSteps = [
  'أرسل رسالة من البريد المسجل أو بريد ولي الأمر إلى بريد دعم الخصوصية الموضح أدناه.',
  `اكتب في عنوان الرسالة «${deletionRequestSubject}».`,
  'اكتب رقم الهاتف المرتبط بالحساب أو كود متابعة ولي الأمر، حتى نتمكن من التحقق من ملكية الطلب. لا ترسل كلمة المرور أو أي رمز تحقق.',
  'سيتواصل فريق الدعم معك عند الحاجة لاستكمال التحقق، ثم يعالج الطلب خلال مدة لا تتجاوز 30 يومًا.',
] as const;

function DeletionStepsCard() {
  return (
    <article className="landing-panel rounded-2xl p-6">
      <h2 className="flex items-center gap-2 text-xl font-black text-[var(--landing-ink)]">
        <ShieldCheck className="h-5 w-5 text-[var(--landing-accent)]" />
        خطوات تقديم الطلب
      </h2>
      <ol className="mt-5 space-y-4 text-sm font-semibold leading-7 text-[var(--landing-muted)]">
        {deletionSteps.map((step, index) => (
          <li key={step} className="flex gap-3">
            <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[var(--landing-teal-soft)] text-xs font-black text-[var(--landing-accent)]">
              {index + 1}
            </span>
            <span>{step}</span>
          </li>
        ))}
      </ol>
      <a
        href={`mailto:${supportEmail}?subject=${encodeURIComponent(deletionRequestSubject)}`}
        className="mt-7 inline-flex min-h-12 items-center justify-center gap-2 rounded-full bg-[var(--landing-accent)] px-6 py-3 text-sm font-extrabold text-[var(--landing-accent-foreground)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-accent-strong)]"
      >
        <Mail className="h-4 w-4" />
        {supportEmail}
      </a>
    </article>
  );
}

function DeletionScopeCard() {
  return (
    <article className="landing-panel mt-5 rounded-2xl p-6">
      <h2 className="text-xl font-black text-[var(--landing-ink)]">
        ما الذي يُحذف وما الذي قد نحتفظ به؟
      </h2>
      <ul className="mt-4 space-y-3 text-sm font-semibold leading-7 text-[var(--landing-muted)]">
        <li>
          نحذف أو نخفي هوية بيانات الملف الشخصي، بيانات الربط، وإشعارات الجهاز
          التي لم تعد لازمة لتقديم الخدمة.
        </li>
        <li>
          يؤدي حذف بيانات الربط إلى توقف ظهور الطالب داخل تطبيق ولي الأمر.
        </li>
        <li>
          قد نحتفظ فقط بالسجلات التي يفرضها القانون أو اللازمة لمنع الاحتيال
          وتسوية المعاملات والنزاعات، ثم نحذفها أو نخفي هويتها بعد انتهاء مدة
          الاحتفاظ النظامية.
        </li>
      </ul>
    </article>
  );
}

function DeletionPageActions() {
  return (
    <div className="mt-10 flex flex-wrap justify-start gap-3">
      <Link
        href="/privacy"
        className="inline-flex items-center gap-2 rounded-full border border-[var(--landing-accent)] px-6 py-3 text-sm font-extrabold text-[var(--landing-accent)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-teal-soft)]"
      >
        قراءة سياسة الخصوصية
        <ShieldCheck className="h-4 w-4" />
      </Link>
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

export default function AccountDeletionPage() {
  return (
    <main className="landing-page min-h-screen">
      <div className="landing-page__backdrop" />
      <div className="landing-page__texture" />

      <section className="relative z-10 mx-auto max-w-4xl px-6 py-24 text-right">
        <div className="mb-10">
          <div className="landing-chip mb-5">
            <Trash2 className="h-4 w-4" />
            <span>حذف الحساب والبيانات</span>
          </div>
          <h1 className="text-4xl font-black leading-tight text-[var(--landing-ink)] md:text-5xl">
            طلب حذف بيانات تطبيق متابعه ولي أمر- مسار اكاديمي
          </h1>
          <p className="mt-4 text-base font-semibold leading-8 text-[var(--landing-muted)]">
            هذه الصفحة تخص تطبيق <strong>متابعه ولي أمر- مسار اكاديمي</strong> (
            <span dir="ltr">com.massar.parent</span>) المنشور بواسطة{' '}
            <span dir="ltr">EGY Legal for smart technology</span>.
          </p>
        </div>

        <DeletionStepsCard />
        <DeletionScopeCard />
        <DeletionPageActions />
      </section>
    </main>
  );
}

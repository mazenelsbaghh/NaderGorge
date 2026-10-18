'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { publicExamsService } from '@/services/public-exams-service';

export default function PublicExamLinkPageClient() {
  const { slug } = useParams<{ slug: string }>();
  const router = useRouter();
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;
    publicExamsService.resolveSlug(slug).then(exam => {
      if (!cancelled) router.replace(`/student/exams/${exam.examId}?from=public-exams`);
    }).catch((failure: unknown) => {
      if (cancelled) return;
      const response = failure as { response?: { status?: number } };
      setError(response.response?.status === 404
        ? 'الامتحان غير متاح لحسابك حالياً. تأكد من الرابط وبياناتك الدراسية، أو تواصل مع الدعم.'
        : 'تعذر فتح الامتحان. أعد تحميل الصفحة للمحاولة مرة أخرى.');
    });
    return () => { cancelled = true; };
  }, [slug, router]);

  return (
    <main className="mx-auto max-w-xl space-y-4 py-12 text-center" dir="rtl">
      <h1 className="text-xl font-bold text-[var(--admin-text)]">{error ? 'الامتحان غير متاح' : 'جارٍ فتح الامتحان...'}</h1>
      <p role="status" className="text-sm leading-7 text-[var(--admin-muted)]">{error || 'نتحقق من إتاحة الامتحان لحسابك.'}</p>
      {error && <Link href="/student/public-exams" className="inline-flex min-h-11 items-center text-sm font-bold text-[var(--admin-primary)]">العودة للامتحانات العامة</Link>}
    </main>
  );
}

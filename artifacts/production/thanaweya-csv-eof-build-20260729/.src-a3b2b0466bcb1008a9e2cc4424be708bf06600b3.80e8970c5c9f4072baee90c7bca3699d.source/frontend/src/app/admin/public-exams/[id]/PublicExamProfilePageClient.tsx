'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { ArrowRight } from 'lucide-react';
import { AdminPageSkeleton, AdminShellChrome } from '@/components/admin';
import ExamProfilePageClient from '../../content/exams/[id]/ExamProfilePageClient';
import { adminSalesService, type PublicExamProductDto } from '@/services/admin-sales-service';

export default function AdminPublicExamProfilePageClient({ productId }: { productId: string }) {
  const [product, setProduct] = useState<PublicExamProductDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    adminSalesService.publicExams()
      .then((exams) => setProduct(exams.find((exam) => exam.id === productId) ?? null))
      .finally(() => setLoading(false));
  }, [productId]);

  if (loading) {
    return (
      <AdminShellChrome activePath="/admin/public-exams" sectionLabel="الامتحانات العامة" pageTitle="تحميل الامتحان">
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  if (!product) {
    return (
      <AdminShellChrome
        activePath="/admin/public-exams"
        sectionLabel="الامتحانات العامة"
        pageTitle="الامتحان غير موجود"
        action={<Link href="/admin/public-exams" className="inline-flex items-center gap-2 rounded-md border border-[var(--admin-border)] px-3 py-2 text-sm font-bold"><ArrowRight className="h-4 w-4" />رجوع</Link>}
      >
        <p className="rounded-lg border border-red-200 bg-red-50 p-4 font-bold text-red-700">لم يتم العثور على الامتحان العام.</p>
      </AdminShellChrome>
    );
  }

  return (
    <ExamProfilePageClient
      id={product.examId}
      activePath="/admin/public-exams"
      sectionLabel="الامتحانات العامة ▸ إدارة الأسئلة"
    />
  );
}

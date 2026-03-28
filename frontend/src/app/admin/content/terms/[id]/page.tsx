'use client';

import { useEffect, useState, useRef, use } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, ArrowRight, Calendar, Folder, Link2 } from 'lucide-react';
import { AdminShellChrome, AdminStatCard, AdminTabBar, AdminTab, SectionListManager, AddSectionForm, SectionListManagerRef, AdminPageSkeleton } from '@/components/admin';
import { adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';

export default function TermProfilePage(props: { params: Promise<{ id: string }> }) {
  const params = use(props.params);
  const router = useRouter();
  const [term, setTerm] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const sectionListRef = useRef<SectionListManagerRef>(null);

  useEffect(() => {
    async function loadData() {
      try {
        const response = await adminService.getTermById(params.id);
        setTerm(response);
      } catch (error) {
        toast.error('تعذر تحميل تفاصيل الترم');
      } finally {
        setLoading(false);
      }
    }
    loadData();
  }, [params.id]);

  if (loading) {
    return (
      <AdminShellChrome
        activePath="/admin/content"
        sectionLabel="إدارة المحتوى"
        pageTitle="جاري التحميل..."
        subtitle="الرجاء الانتظار"
      >
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  if (!term) {
     return (
        <AdminShellChrome
            activePath="/admin/content"
            sectionLabel="إدارة المحتوى"
            pageTitle="خطأ"
            subtitle="الترم غير موجود"
        >
            <div className="p-8 text-center text-[var(--admin-muted)]">
                لا يمكن العثور على الترم المطلوب
            </div>
            <div className="flex justify-center mt-4">
                <button
                    onClick={() => router.back()}
                    className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-card-strong)] px-6 py-3 text-sm font-bold text-[var(--admin-text)] shadow-sm border border-[var(--admin-border)] transition hover:bg-[var(--admin-hover)]"
                >
                    <ArrowRight className="h-4 w-4" /> عودة للخلف
                </button>
            </div>
        </AdminShellChrome>
     )
  }

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى ▸ الباقات ▸ الأترام"
      pageTitle={term.title}
      subtitle={`الترتيب: ${term.order}`}
      action={
        <button
          onClick={() => router.push(`/admin/content/packages/${term.packageId}`)}
          className="inline-flex w-fit items-center gap-2 rounded-full bg-[var(--admin-card-strong)] px-6 py-3 text-sm font-bold text-[var(--admin-text)] shadow-sm border border-[var(--admin-border)] transition hover:bg-[var(--admin-hover)]"
        >
          <ArrowRight className="h-4 w-4" /> عودة للباقة
        </button>
      }
    >

      <div className="space-y-6 mt-6">
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <h3 className="mb-4 flex items-center gap-2 text-xl font-bold text-[var(--admin-text)]">
             <Folder className="h-5 w-5 text-[var(--admin-primary)]" /> إدارة الأقسام
          </h3>
          <SectionListManager termId={term.id} ref={sectionListRef} />
        </div>
        <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
          <h3 className="mb-4 text-lg font-bold text-[var(--admin-text)]">إضافة قسم جديد</h3>
          <AddSectionForm termId={term.id} onSuccess={() => sectionListRef.current?.reload()} />
        </div>
      </div>

    </AdminShellChrome>
  );
}

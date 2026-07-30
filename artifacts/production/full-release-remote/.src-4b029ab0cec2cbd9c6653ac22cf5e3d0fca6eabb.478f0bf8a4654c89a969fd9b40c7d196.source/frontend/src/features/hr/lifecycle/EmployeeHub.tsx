'use client';

import Link from 'next/link';
import { useCallback, useEffect, useState } from 'react';
import {
  ArrowLeft,
  CalendarCheck,
  CalendarDays,
  Download,
  FileText,
  Landmark,
  Loader2,
  PackageCheck,
  ReceiptText,
  RefreshCw,
} from 'lucide-react';
import toast from 'react-hot-toast';
import {
  EmployeeAssetDto,
  EmployeeDocumentDto,
  hrLifecycleService,
} from '@/services/hr-lifecycle-service';
import { HrStatusBadge } from '@/features/hr/components/HrStatusBadge';

const services = [
  {
    href: '/assistant/attendance',
    label: 'الحضور والانصراف',
    description: 'سجّل يومك وراجع التصحيحات',
    icon: CalendarCheck,
  },
  {
    href: '/assistant/vacations',
    label: 'الإجازات',
    description: 'الأرصدة والطلبات والموافقات',
    icon: CalendarDays,
  },
  {
    href: '/assistant/payroll',
    label: 'كشوف الرواتب',
    description: 'الصافي وشرح كل بند',
    icon: ReceiptText,
  },
  {
    href: '/assistant/financial-requests',
    label: 'الطلبات المالية',
    description: 'السلف والقروض والمصروفات',
    icon: Landmark,
  },
];

export function EmployeeHub() {
  const [documents, setDocuments] = useState<EmployeeDocumentDto[]>([]);
  const [assets, setAssets] = useState<EmployeeAssetDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setFailed(false);
    try {
      const [docs, custody] = await Promise.all([
        hrLifecycleService.myDocuments(),
        hrLifecycleService.myAssets(),
      ]);
      setDocuments(docs);
      setAssets(custody);
    } catch {
      setFailed(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function download(id: string) {
    try {
      const reference = await hrLifecycleService.downloadDocument(id);
      toast.success(`تم تجهيز المستند: ${reference}`);
    } catch {
      toast.error('لا تملك صلاحية تنزيل هذا المستند');
    }
  }

  return (
    <div className="space-y-8">
      <section aria-label="خدمات الموظف" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {services.map(({ href, label, description, icon: Icon }) => (
          <Link key={href} href={href} className="hr-service-link group">
            <span className="hr-icon">
              <Icon className="h-5 w-5" aria-hidden="true" />
            </span>
            <h2 className="mt-4 font-black">{label}</h2>
            <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">{description}</p>
            <ArrowLeft
              className="absolute bottom-5 left-5 h-4 w-4 text-[var(--admin-accent)] transition-transform group-hover:-translate-x-1"
              aria-hidden="true"
            />
          </Link>
        ))}
      </section>

      {loading ? (
        <div className="hr-loading" role="status">
          <Loader2 className="mx-auto h-6 w-6 animate-spin text-[var(--admin-accent)]" />
          <p className="mt-3">جارٍ تحميل مستنداتك وعُهدك…</p>
        </div>
      ) : failed ? (
        <div role="alert" className="hr-panel border-[var(--admin-danger-20)] text-center">
          <p className="font-black text-[var(--admin-danger)]">تعذر تحميل المستندات والعُهد.</p>
          <p className="mt-2 text-sm text-[var(--admin-muted)]">تحقق من الاتصال ثم أعد المحاولة.</p>
          <button onClick={() => void load()} className="admin-btn-ghost mt-4 min-h-11">
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            إعادة المحاولة
          </button>
        </div>
      ) : (
        <div className="grid gap-5 lg:grid-cols-2">
          <section className="hr-panel" aria-labelledby="documents-heading">
            <div className="flex items-center gap-3">
              <span className="hr-icon">
                <FileText className="h-5 w-5" aria-hidden="true" />
              </span>
              <div>
                <h2 id="documents-heading" className="text-lg font-black">مستنداتي</h2>
                <p className="text-sm text-[var(--admin-muted)]">{documents.length} مستند متاح</p>
              </div>
            </div>
            <div className="mt-5 space-y-2">
              {documents.length === 0 ? (
                <p className="hr-empty">لا توجد مستندات منشورة لك حتى الآن.</p>
              ) : documents.map((document) => (
                <article
                  key={document.id}
                  className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--admin-border)] py-4 last:border-b-0"
                >
                  <div className="min-w-0">
                    <p className="truncate font-black">{document.name}</p>
                    <p className="mt-1 text-xs text-[var(--admin-muted)]">
                      {document.category} · نسخة {document.latestVersion ?? '—'}
                      {document.expiresOn ? ` · ينتهي ${document.expiresOn}` : ''}
                    </p>
                  </div>
                  <button
                    onClick={() => void download(document.id)}
                    className="admin-btn-ghost min-h-11"
                    aria-label={`تنزيل ${document.name}`}
                  >
                    <Download className="h-4 w-4" aria-hidden="true" />
                    تنزيل
                  </button>
                </article>
              ))}
            </div>
          </section>

          <section className="hr-panel" aria-labelledby="assets-heading">
            <div className="flex items-center gap-3">
              <span className="hr-icon">
                <PackageCheck className="h-5 w-5" aria-hidden="true" />
              </span>
              <div>
                <h2 id="assets-heading" className="text-lg font-black">عُهدي</h2>
                <p className="text-sm text-[var(--admin-muted)]">{assets.length} عهدة مسجلة</p>
              </div>
            </div>
            <div className="mt-5 space-y-2">
              {assets.length === 0 ? (
                <p className="hr-empty">لا توجد عُهد مسجلة عليك.</p>
              ) : assets.map((asset) => (
                <article
                  key={asset.id}
                  className="border-b border-[var(--admin-border)] py-4 last:border-b-0"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="truncate font-black">{asset.asset}</p>
                      <p className="mt-1 text-xs text-[var(--admin-muted)]">
                        {asset.code}{asset.serialNumber ? ` · ${asset.serialNumber}` : ''}
                      </p>
                    </div>
                    <HrStatusBadge status={asset.state} />
                  </div>
                  <p className="mt-3 text-sm text-[var(--admin-muted)]">
                    الحالة عند الاستلام: {asset.assignedCondition}
                  </p>
                </article>
              ))}
            </div>
          </section>
        </div>
      )}
    </div>
  );
}

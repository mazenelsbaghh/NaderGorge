'use client';

import { devConsole } from '@/utils/dev-console';
import { useEffect, useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowRight, Download, KeyRound, Printer, Search, Sparkles, User as UserIcon, Link as LinkIcon } from 'lucide-react';
import Link from 'next/link';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminPageSkeleton,
} from '@/components/admin';
import { formatDate } from '@/components/admin/admin-utils';
import { adminService, CodeDetailDto, CodeGroupDto } from '@/services/admin-service';
import { PackageDto, contentService } from '@/services/content-service';
import { QrDisplay } from '@/components/codes/QrDisplay';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

export default function CodeGroupDetailsPage() {
  const params = useParams();
  const router = useRouter();
  const groupId = params.groupId as string;

  const [group, setGroup] = useState<CodeGroupDto | null>(null);
  const [codes, setCodes] = useState<CodeDetailDto[]>([]);
  const [packages, setPackages] = useState<PackageDto[]>([]);
  
  const [loading, setLoading] = useState(true);
  const [codesLoading, setCodesLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [showQrPrint, setShowQrPrint] = useState(false);

  useEffect(() => {
    async function loadGroupData() {
      try {
        setLoading(true);
        const [groupsData, packagesResponse] = await Promise.all([
          adminService.listCodeGroups(),
          contentService.getPackages(),
        ]);

        const foundGroup = groupsData?.find((g) => g.id === groupId);
        if (foundGroup) {
          setGroup(foundGroup);
        } else {
          toast.error('مجموعة الأكواد غير موجودة');
          router.push('/admin/codes');
          return;
        }
        
        setPackages((packagesResponse.data?.data || []) as PackageDto[]);
      } catch (error) {
        devConsole.error(error);
        toast.error('تعذر تحميل بيانات المجموعة');
      } finally {
        setLoading(false);
      }

      try {
        setCodesLoading(true);
        const data = await adminService.getCodeGroupDetails(groupId);
        setCodes(data);
      } catch (error) {
        devConsole.error(error);
        toast.error('تعذر تحميل الأكواد');
      } finally {
        setCodesLoading(false);
      }
    }

    if (groupId) {
      void loadGroupData();
    }
  }, [groupId, router]);

  const packageNameMap = useMemo(() => {
    return Object.fromEntries(packages.map((pkg) => [pkg.id, pkg.name]));
  }, [packages]);

  const filteredCodes = useMemo(() => {
    if (!searchQuery.trim()) return codes;
    const q = searchQuery.toLowerCase().trim();
    return codes.filter((c) => 
      c.code.toLowerCase().includes(q) ||
      (c.usedByUserId && c.usedByUserId.toLowerCase().includes(q)) ||
      (c.usedByStudentName && c.usedByStudentName.toLowerCase().includes(q)) ||
      (c.usedByStudentPhone && c.usedByStudentPhone.toLowerCase().includes(q))
    );
  }, [codes, searchQuery]);

  function exportCsv() {
    if (!group || codes.length === 0) return;

    const header = 'Code,IsUsed,UsedAt,UsedByUserId,StudentName,StudentPhone\n';
    const rows = codes
      .map(
        (code) =>
          `"${code.code}","${code.isUsed}","${code.usedAt ? formatDate(code.usedAt) : ''}","${
            code.usedByUserId || ''
          }","${code.usedByStudentName || ''}","${code.usedByStudentPhone || ''}"`
      )
      .join('\n');
    const blob = new Blob([header + rows], { type: 'text/csv;charset=utf-8;' });
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `codes_${group.name || group.id}.csv`;
    anchor.click();
    window.URL.revokeObjectURL(url);
  }

  const codeColumns: AdminColumn<CodeDetailDto>[] = [
    {
      key: 'code',
      label: 'الكود',
      render: (c) => (
        <span className="bg-[var(--admin-card-strong)] px-3 py-1.5 rounded-xl border border-[var(--admin-border)] font-mono font-bold text-[var(--admin-text)] tracking-wider">
          {c.code}
        </span>
      ),
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (c) =>
        c.isUsed ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 px-3 py-1 text-xs font-bold">
            <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
            مستخدم
          </span>
        ) : (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-card-strong)] text-[var(--admin-muted)] px-3 py-1 text-xs font-bold border border-[var(--admin-border)]">
            <span className="h-1.5 w-1.5 rounded-full bg-zinc-400" />
            جديد
          </span>
        ),
    },
    {
      key: 'usedAt',
      label: 'وقت الاستخدام',
      render: (c) => <span className="text-sm text-[var(--admin-muted)] font-medium">{c.usedAt ? formatDate(c.usedAt) : '—'}</span>,
    },
    {
      key: 'usedBy',
      label: 'المستخدم / الطالب',
      render: (c) => {
        if (!c.isUsed || !c.usedByUserId) return <span className="text-[var(--admin-muted)]">—</span>;
        return (
          <Link href={`/admin/users/${c.usedByUserId}`} className="group inline-flex flex-col items-start hover:underline">
            <span className="font-bold text-[var(--admin-primary)] group-hover:text-[var(--admin-primary-strong)] flex items-center gap-1">
              <UserIcon size={14} className="text-[var(--admin-primary)] animate-pulse" />
              {c.usedByStudentName || 'طالب مجهول الاسم'}
            </span>
            {c.usedByStudentPhone && (
              <span className="text-[10px] text-[var(--admin-muted)] font-mono mt-0.5">{c.usedByStudentPhone}</span>
            )}
          </Link>
        );
      },
    },
  ];

  if (loading) {
    return (
      <AdminShellChrome
        activePath="/admin/codes"
        sectionLabel="إدارة الأكواد"
        pageTitle="تفاصيل مجموعة الأكواد"
        subtitle="جاري تحميل البيانات..."
      >
        <AdminPageSkeleton />
      </AdminShellChrome>
    );
  }

  return (
    <AdminShellChrome
      activePath="/admin/codes"
      sectionLabel="إدارة الأكواد"
      pageTitle={group ? `تفاصيل: ${group.name || 'دفعة أكواد'}` : 'تفاصيل المجموعة'}
      subtitle="استعراض الأكواد، سجل الشحن، الربط، وطباعة كود الـ QR."
      action={
        <Link href="/admin/codes" passHref legacyBehavior>
          <NeumorphButton intent="ghost" size="md">
            <ArrowRight className="h-4 w-4 ml-1.5" />
            العودة للمجموعات
          </NeumorphButton>
        </Link>
      }
    >
      {/* Stats / Info cards */}
      {group && (
        <section className="mb-10 grid grid-cols-1 gap-6 md:grid-cols-3">
          <AdminStatCard
            variant="light"
            icon={KeyRound}
            label="إجمالي الأكواد"
            value={group.codeCount}
            subtitle={`تاريخ التوليد: ${formatDate(group.createdAt)}`}
          />
          <AdminStatCard
            variant="accent"
            icon={Sparkles}
            label="المستخدمة"
            value={group.usedCount}
            subtitle={`${group.codeCount - group.usedCount} أكواد متبقية`}
          />
          <AdminStatCard
            variant="muted"
            icon={LinkIcon}
            label="الربط"
            value={group.packageId ? 'باقة تعليمية' : 'عام'}
            subtitle={group.packageId ? packageNameMap[group.packageId] || group.packageId : 'وصول عام للمنصة'}
          />
        </section>
      )}

      {/* Toolbar / Search & Actions */}
      <div className="mb-8 flex flex-col md:flex-row gap-4 justify-between items-center bg-[var(--admin-card)] p-4 rounded-3xl border border-[var(--admin-border)] shadow-sm">
        
        {/* Search Input */}
        <div className="flex items-center bg-[var(--admin-surface)] rounded-2xl border border-[var(--admin-border)] px-4 py-2.5 w-full md:max-w-md">
          <Search className="text-[var(--admin-muted)] w-5 h-5 ml-2.5" />
          <input
            type="text"
            placeholder="ابحث عن كود، اسم طالب، أو رقم هاتف..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="bg-transparent border-none outline-none text-sm text-[var(--admin-text)] placeholder:text-[var(--admin-muted)] w-full text-right"
            dir="rtl"
          />
        </div>

        {/* Action Tabs & Buttons */}
        <div className="flex flex-wrap gap-3 items-center justify-end w-full md:w-auto">
          <div className="flex gap-1.5 p-1 bg-[var(--admin-surface)] rounded-2xl border border-[var(--admin-border)]">
            <button
              onClick={() => setShowQrPrint(false)}
              className={`px-5 py-2 rounded-xl text-sm font-bold transition-all ${
                !showQrPrint 
                  ? 'bg-[var(--admin-primary)] text-white shadow-sm' 
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              سجل الشحن والتفاصيل
            </button>
            <button
              onClick={() => setShowQrPrint(true)}
              className={`px-5 py-2 rounded-xl text-sm font-bold flex items-center gap-2 transition-all ${
                showQrPrint 
                  ? 'bg-[var(--admin-primary)] text-white shadow-sm' 
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              <Printer size={16} />
              طباعة QR
            </button>
          </div>

          <NeumorphButton type="button" onClick={exportCsv} intent="ghost" size="md">
            <Download className="h-4 w-4 ml-1.5" />
            تصدير CSV
          </NeumorphButton>
        </div>
      </div>

      {/* Content Area */}
      <div className="admin-panel mt-6">
        {showQrPrint ? (
          <QrDisplay
            codes={codes.map((c) => c.code)}
            groupName={group ? `${group.name || 'دفعة'} - ${formatDate(group.createdAt)}` : 'Batch'}
          />
        ) : (
          <AdminDataTable
            data={filteredCodes}
            columns={codeColumns}
            loading={codesLoading}
            rowKey={(c) => c.code}
            emptyMessage="لا توجد أكواد تطابق البحث."
          />
        )}
      </div>
    </AdminShellChrome>
  );
}

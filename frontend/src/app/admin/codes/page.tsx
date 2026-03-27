'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Download, Eye, KeyRound, Plus, Sparkles, Printer, Layers } from 'lucide-react';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminModal,
} from '@/components/admin';
import { formatCompactNumber, formatDate } from '@/components/admin/admin-utils';
import { adminService, CodeDetailDto, CodeGroupDto } from '@/services/admin-service';
import { PackageDto, contentService } from '@/services/content-service';
import { codeService } from '@/services/code-service';
import { CodeTypeSelector, CodeTypeSelection } from '@/components/codes/CodeTypeSelector';
import { QrDisplay } from '@/components/codes/QrDisplay';
import toast from 'react-hot-toast';

export default function AdminCodesPage() {
  const [groups, setGroups] = useState<CodeGroupDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showGenModal, setShowGenModal] = useState(false);
  
  // Generation Form State
  const [genCount, setGenCount] = useState(10);
  const [genSelection, setGenSelection] = useState<CodeTypeSelection>({ codeType: 'Package' });
  const [genGroupName, setGenGroupName] = useState('');
  const [genLoading, setGenLoading] = useState(false);

  // Details Modal State
  const [selectedGroup, setSelectedGroup] = useState<CodeGroupDto | null>(null);
  const [codes, setCodes] = useState<CodeDetailDto[]>([]);
  const [codesLoading, setCodesLoading] = useState(false);
  const [showQrPrint, setShowQrPrint] = useState(false);

  const [packages, setPackages] = useState<PackageDto[]>([]);

  useEffect(() => {
    void loadData();
  }, []);

  async function loadData() {
    try {
      setLoading(true);
      const [groupsData, packagesResponse] = await Promise.all([
        adminService.listCodeGroups(),
        contentService.getPackages(),
      ]);
      setGroups(groupsData);
      setPackages((packagesResponse.data?.data || []) as PackageDto[]);
    } catch (error) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  }

  async function handleGenerate(event: FormEvent) {
    event.preventDefault();
    
    try {
      setGenLoading(true);
      
      await codeService.createCodeGroup({
        groupName: genGroupName,
        codeType: genSelection.codeType,
        count: genCount,
        codeLength: 12,
        packageId: genSelection.packageId || undefined,
        termId: genSelection.termId || undefined,
        contentSectionId: genSelection.contentSectionId || undefined,
        lessonId: genSelection.lessonId || undefined,
        examId: genSelection.examId || undefined,
        videoTargetIds: genSelection.videoTargetIds && genSelection.videoTargetIds.length > 0 ? genSelection.videoTargetIds : undefined,
        balanceAmount: genSelection.balanceAmount || undefined,
        discountPercentage: genSelection.discountPercentage || undefined,
        expiresAt: genSelection.expiresAt || undefined,
      });

      toast.success('تم التوليد بنجاح!');
      setShowGenModal(false);
      setGenSelection({ codeType: 'Package' });
      setGenGroupName('');
      setGenCount(10);
      await loadData();
    } catch (error: any) {
      console.error(error);
      const msg = error.response?.data?.message || 'تعذر إنشاء الأكواد. تأكد من إدخال جميع الحقول المطلوبة.';
      toast.error(msg);
    } finally {
      setGenLoading(false);
    }
  }

  async function openGroupDetails(group: CodeGroupDto) {
    setSelectedGroup(group);
    setShowQrPrint(false); // Reset to table view

    try {
      setCodesLoading(true);
      const data = await adminService.getCodeGroupDetails(group.id);
      setCodes(data);
    } catch (error) {
      console.error(error);
      toast.error('تعذر تحميل تفاصيل المجموعة');
    } finally {
      setCodesLoading(false);
    }
  }

  function exportCsv() {
    if (!selectedGroup || codes.length === 0) return;

    const header = 'Code,IsUsed,UsedAt,UsedByUserId\n';
    const rows = codes
      .map((code) => `${code.code},${code.isUsed},${code.usedAt ? formatDate(code.usedAt) : ''},${code.usedByUserId || ''}`)
      .join('\n');
    const blob = new Blob([header + rows], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `codes_${selectedGroup.id}.csv`;
    anchor.click();
    window.URL.revokeObjectURL(url);
  }

  const packageNameMap = useMemo(() => {
    return Object.fromEntries(packages.map((pkg) => [pkg.id, pkg.name]));
  }, [packages]);

  const totalCodes = groups.reduce((sum, group) => sum + group.codeCount, 0);
  const usedCodes = groups.reduce((sum, group) => sum + group.usedCount, 0);

  const groupColumns: AdminColumn<CodeGroupDto>[] = [
    {
      key: 'createdAt',
      label: 'تاريخ الإنشاء',
      render: (g) => <span className="text-[var(--admin-muted)]">{formatDate(g.createdAt)}</span>,
    },
    {
      key: 'linking',
      label: 'الربط',
      render: (g: any) => (
        <div className="font-semibold text-[var(--admin-text)]">
          <div>{g.codeType ? g.codeType : 'كود عام'}</div>
          {g.packageId ? (
            <div className="mt-1 text-xs text-[var(--admin-muted)] font-normal">{packageNameMap[g.packageId] || g.packageId}</div>
          ) : null}
        </div>
      ),
    },
    {
      key: 'usage',
      label: 'الاستخدام',
      render: (g) => (
        <div>
          <div className="text-sm font-bold text-[var(--admin-text)]">
            {formatCompactNumber(g.usedCount)} / {formatCompactNumber(g.codeCount)}
          </div>
          <div className="mt-2 h-1.5 rounded-full bg-[var(--admin-card-strong)] overflow-hidden border border-[var(--admin-border)]">
            <div
              className="h-full rounded-full bg-[var(--admin-primary-strong)]"
              style={{ width: `${g.codeCount === 0 ? 0 : (g.usedCount / g.codeCount) * 100}%` }}
            />
          </div>
        </div>
      ),
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (g) => (
        <div className="flex items-center justify-end gap-2">
          <button
            onClick={() => openGroupDetails(g)}
            className="admin-btn-icon"
            title="عرض التفاصيل والطباعة"
          >
            <Eye className="h-5 w-5" />
          </button>
        </div>
      ),
    },
  ];

  const codeColumns: AdminColumn<CodeDetailDto>[] = [
    {
      key: 'code',
      label: 'الكود',
      render: (c) => (
        <span className="bg-[var(--admin-card-strong)] px-2 py-1 rounded-md border border-[var(--admin-border)] font-mono font-bold text-[var(--admin-text)] tracking-wider">
          {c.code}
        </span>
      ),
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (c) =>
        c.isUsed ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 text-emerald-700 px-3 py-1 text-xs font-bold dark:bg-emerald-950/40 dark:text-emerald-400">
            <span className="h-1.5 w-1.5 rounded-full bg-current" />
            مستخدم
          </span>
        ) : (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-card-strong)] text-[var(--admin-muted)] px-3 py-1 text-xs font-bold">
            <span className="h-1.5 w-1.5 rounded-full bg-current" />
            جديد
          </span>
        ),
    },
    {
      key: 'usedAt',
      label: 'وقت الاستخدام',
      render: (c) => <span className="text-[var(--admin-muted)]">{c.usedAt ? formatDate(c.usedAt) : '-'}</span>,
    },
    {
      key: 'usedBy',
      label: 'المستخدم',
      render: (c) => <span className="font-medium text-[var(--admin-text)]">{c.usedByUserId || '-'}</span>,
    },
  ];

  return (
    <AdminShellChrome
      activePath="/admin/codes"
      sectionLabel="إدارة الأكواد"
      pageTitle="مجموعات أكواد الوصول"
      subtitle="إدارة التوليد والطباعة (QR) والاستخدام في شاشة واحدة."
      action={
        <button
          onClick={() => setShowGenModal(true)}
          className="inline-flex w-fit items-center gap-2 rounded-full bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] px-8 py-4 text-sm font-bold text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)] transition hover:brightness-110"
        >
          <Plus className="h-4 w-4" />
          إنشاء دفعة جديدة
        </button>
      }
    >
      {/* Mobile Fab */}
      <button
        onClick={() => setShowGenModal(true)}
        className="fixed bottom-8 left-8 z-40 flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-2xl transition hover:scale-110 md:hidden"
      >
        <Plus className="h-5 w-5" />
      </button>

      {/* Stats */}
      <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={KeyRound}
          label="إجمالي الأكواد"
          value={totalCodes}
        />
        <AdminStatCard
          variant="accent"
          icon={Sparkles}
          label="المستخدمة"
          value={usedCodes}
        />
        <AdminStatCard
          variant="muted"
          icon={Layers} // Wait Layers undefined, Using Box/Layers? Let's use Sparkles for now
          label="المجموعات"
          value={groups.length}
        />
      </section>

      {/* Code Groups Table */}
      <AdminDataTable
        data={groups}
        columns={groupColumns}
        loading={loading}
        rowKey={(g) => g.id}
        emptyMessage="لا توجد مجموعات أكواد بعد."
      />

      {/* Generation Modal */}
      <AdminModal
        open={showGenModal}
        onClose={() => setShowGenModal(false)}
        title="إنشاء دفعة أكواد"
        subtitle="توليد دفعة جديدة مع تحديد نوع الوصول"
        maxWidth="max-w-4xl"
      >
        <form onSubmit={handleGenerate} className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">اسم المجموعة (اختياري)</label>
              <input
                type="text"
                value={genGroupName}
                onChange={(e) => setGenGroupName(e.target.value)}
                className="admin-input"
                placeholder="مثلاً: دفعة الكورس المكثف"
                dir="auto"
              />
            </div>
            <div>
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">عدد الأكواد للمجموعة</label>
              <input
                type="number"
                min={1}
                max={10000}
                value={genCount}
                onChange={(e) => setGenCount(Number(e.target.value))}
                className="admin-input"
                placeholder="عدد الأكواد (مثلا: 100)"
                required
              />
            </div>
          </div>

          <div className="pt-4 border-t border-[var(--admin-border)]">
            <CodeTypeSelector
              value={genSelection}
              onChange={setGenSelection}
            />
          </div>

          <div className="flex justify-end gap-3 pt-4 border-t border-[var(--admin-border)]">
            <button type="button" onClick={() => setShowGenModal(false)} className="px-4 py-2 font-semibold text-[var(--admin-muted)] transition hover:text-[var(--admin-text)]">إلغاء</button>
            <button type="submit" disabled={genLoading} className="admin-btn-primary">
              {genLoading ? 'جارٍ التوليد...' : 'توليد الدفعة'}
            </button>
          </div>
        </form>
      </AdminModal>

      {/* Group Details / QR Print Modal */}
      <AdminModal
        open={!!selectedGroup}
        onClose={() => setSelectedGroup(null)}
        title={showQrPrint ? "طباعة أكواد QR" : "تفاصيل المجموعة"}
        subtitle={`مجموعة: ${selectedGroup?.id}`}
        maxWidth="max-w-5xl"
      >
        <div className="mb-6 flex flex-wrap gap-2 justify-between items-center bg-[var(--admin-card-soft)] p-2 rounded-xl border border-[var(--admin-border)]">
          
          <div className="flex gap-2 p-1 bg-[var(--admin-bg)] rounded-lg border border-[var(--admin-border)]">
            <button
              onClick={() => setShowQrPrint(false)}
              className={`px-4 py-2 rounded-md text-sm font-bold transition-all ${!showQrPrint ? 'bg-[var(--admin-primary)] text-white shadow-sm' : 'text-[var(--admin-muted)] hover:text-white'}`}
            >
              عرض السجل
            </button>
            <button
              onClick={() => setShowQrPrint(true)}
              className={`px-4 py-2 rounded-md text-sm font-bold flex items-center gap-2 transition-all ${showQrPrint ? 'bg-[var(--admin-primary)] text-white shadow-sm' : 'text-[var(--admin-muted)] hover:text-white'}`}
            >
              <Printer size={16} />
              طباعة QR
            </button>
          </div>

          <button onClick={exportCsv} className="inline-flex items-center gap-2 rounded-md bg-[var(--admin-card-strong)] border border-[var(--admin-border)] hover:border-[var(--admin-primary)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] transition-all">
            <Download className="h-4 w-4" />
            تصدير CSV
          </button>
        </div>

        {showQrPrint ? (
          <QrDisplay 
            codes={codes.map(c => c.code)} 
            groupName={selectedGroup ? `دفعة ${formatDate(selectedGroup.createdAt)}` : 'Batch'} 
          />
        ) : (
          <AdminDataTable
            data={codes}
            columns={codeColumns}
            loading={codesLoading}
            rowKey={(c) => c.code}
            emptyMessage="لا توجد أكواد لعرضها."
          />
        )}
      </AdminModal>
    </AdminShellChrome>
  );
}

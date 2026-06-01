'use client';

import { useEffect, useState, useCallback } from 'react';
import Link from 'next/link';
import {
  BookOpenText, Plus, ChevronLeft, Sparkles, Video, Search, Trash2,
} from 'lucide-react';
import { AdminShellChrome, AdminPageSkeleton, AdminStatCard } from '@/components/admin';
import { contentService, PackageDto } from '@/services/content-service';
import { adminService } from '@/services/admin-service';
import { ContentHierarchyPanel } from '@/components/admin/ContentHierarchyPanel';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';

// ─── Create Package Inline Form ───────────────────────────────────────────────
function CreatePackageRow({ onSuccess }: { onSuccess: () => void }) {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [price, setPrice] = useState('');
  const [saving, setSaving] = useState(false);

  async function handleCreate() {
    if (!name.trim()) return;
    try {
      setSaving(true);
      await adminService.createPackage({ name: name.trim(), description: description.trim(), price: Number(price) || 0 });
      toast.success('تمت إضافة الباقة بنجاح.');
      setName(''); setDescription(''); setPrice('');
      setOpen(false);
      onSuccess();
    } catch {
      toast.error('حدث خطأ أثناء الإضافة.');
    } finally {
      setSaving(false);
    }
  }

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="flex w-full items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-[var(--admin-border)] bg-transparent py-5 text-sm font-bold text-[var(--admin-muted)] transition hover:border-[var(--admin-primary)] hover:text-[var(--admin-primary)] hover:bg-[var(--admin-primary-15)]/20"
      >
        <Plus className="h-4 w-4" />
        إضافة باقة جديدة
      </button>
    );
  }

  return (
    <div className="rounded-2xl border-2 border-dashed border-[var(--admin-primary)] bg-[var(--admin-primary-15)]/30 p-5 space-y-3">
      <p className="text-sm font-black text-[var(--admin-primary)]">باقة جديدة</p>
      <input
        autoFocus
        type="text"
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="اسم الباقة، مثال: الباقة التأسيسية للأول الثانوي"
        className="admin-input"
        onKeyDown={(e) => { if (e.key === 'Escape') setOpen(false); }}
      />
      <textarea
        rows={2}
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        placeholder="وصف مختصر للباقة..."
        className="admin-input resize-none"
      />
      <input
        type="number"
        min={0}
        value={price}
        onChange={(e) => setPrice(e.target.value)}
        placeholder="السعر (جنيه مصري)"
        className="admin-input"
      />
      <div className="flex justify-end gap-2 pt-1">
        <button
          onClick={() => setOpen(false)}
          className="rounded-xl border border-[var(--admin-border)] px-4 py-2 text-sm font-bold text-[var(--admin-muted)] hover:bg-[var(--admin-card-strong)] transition"
        >
          إلغاء
        </button>
        <NeumorphButton
          onClick={() => void handleCreate()}
          disabled={saving || !name.trim()}
          loading={saving}
          intent="primary"
          size="md"
          pill
        >
          حفظ الباقة
        </NeumorphButton>
      </div>
    </div>
  );
}

// ─── Package Card ─────────────────────────────────────────────────────────────
function PackageCard({ pkg }: { pkg: PackageDto }) {
  return (
    <Link
      href={`/admin/content/packages/${pkg.id}`}
      className="group flex items-center gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-5 py-4 shadow-sm transition-all hover:border-[var(--admin-primary)] hover:shadow-[0_0_0_1px_var(--admin-primary)] hover:bg-[var(--admin-card)]"
    >
      {/* Avatar */}
      <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-lg font-black text-[var(--admin-primary)]">
        {pkg.name.trim()[0]}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <p className="font-black text-[var(--admin-text)] leading-tight truncate">{pkg.name}</p>
        {pkg.description && (
          <p className="text-xs text-[var(--admin-muted)] mt-0.5 line-clamp-1">{pkg.description}</p>
        )}
        <p className="text-xs font-bold text-[var(--admin-primary)] mt-1">{pkg.price} جنيه</p>
      </div>

      {/* Arrow */}
      <ChevronLeft className="h-5 w-5 text-[var(--admin-muted)] opacity-0 group-hover:opacity-100 transition-opacity shrink-0" />
    </Link>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────
export default function AdminContentPage() {
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');

  const loadPackages = useCallback(async () => {
    try {
      setLoading(true);
      const res = await contentService.getPackages();
      setPackages(res.data?.data ?? []);
    } catch {
      toast.error('تعذر تحميل الباقات.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void loadPackages(); }, [loadPackages]);

  const filtered = search.trim()
    ? packages.filter((p) => p.name.toLowerCase().includes(search.toLowerCase()))
    : packages;

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى"
      pageTitle="الباقات التعليمية"
      subtitle="كل باقة تحتوي على أترام وأقسام وحصص ودروس"
    >
      {loading ? (
        <AdminPageSkeleton />
      ) : (
        <div className="space-y-8">
          {/* Stats */}
          <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
            <AdminStatCard variant="accent" icon={BookOpenText} label="إجمالي الباقات" value={packages.length} />
            <AdminStatCard variant="light" icon={Sparkles} label="إجمالي الإيرادات" value={`${packages.reduce((s, p) => s + p.price, 0)} ج`} />
            <AdminStatCard variant="muted" icon={Video} label="نشطة" value={packages.length} />
          </div>

          {/* Search */}
          {packages.length > 3 && (
            <div className="relative">
              <Search className="absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="ابحث في الباقات..."
                className="admin-input pr-11"
              />
            </div>
          )}

          {/* Package list */}
          <div className="space-y-3">
            {filtered.map((pkg) => <PackageCard key={pkg.id} pkg={pkg} />)}
            <CreatePackageRow onSuccess={loadPackages} />
          </div>
        </div>
      )}
    </AdminShellChrome>
  );
}

'use client';

import { useState, useEffect } from 'react';
import { 
  Plus, 
  Smartphone, 
  Activity, 
  TrendingUp, 
  Copy, 
  RefreshCw, 
  Edit2, 
  Wifi,
  WifiOff
} from 'lucide-react';
import { 
  AdminShellChrome, 
  AdminDataTable, 
  AdminColumn,
  AdminStatCard,
  AdminModal
} from '@/components/admin';
import { formatRelativeDate } from '@/components/admin/admin-utils';
import NeumorphButton from '@/components/ui/neumorph-button';
import { walletService, type WalletDto, type CreateWalletDto, type UpdateWalletLimitsDto } from '@/services/wallet-service';
import toast from 'react-hot-toast';

export default function AdminWalletsPageClient() {
  const [wallets, setWallets] = useState<WalletDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [error, setError] = useState('');

  // Modals state
  const [activeModal, setActiveModal] = useState<'add' | 'edit' | null>(null);
  const [selectedWallet, setSelectedWallet] = useState<WalletDto | null>(null);

  // Form states
  const [phoneNumber, setPhoneNumber] = useState('');
  const [label, setLabel] = useState('');
  const [dailyLimit, setDailyLimit] = useState(30000);
  const [monthlyLimit, setMonthlyLimit] = useState(100000);
  const [smsSenderFiltersText, setSmsSenderFiltersText] = useState('VodafoneCash, EtisalatCash, OrangeCash');

  useEffect(() => {
    fetchWallets();
  }, []);

  const fetchWallets = async () => {
    try {
      setLoading(true);
      setError('');
      const data = await walletService.getWallets();
      setWallets(data || []);
    } catch (err: any) {
      console.error(err);
      setError('فشل في تحميل المحافظ الرقمية.');
    } finally {
      setLoading(false);
    }
  };

  const handleToggleActive = async (walletId: string, currentStatus: boolean) => {
    setActionLoading(walletId);
    try {
      const newStatus = !currentStatus;
      await walletService.toggleWallet(walletId, newStatus);
      setWallets(prev => 
        prev.map(w => w.id === walletId ? { ...w, isActive: newStatus } : w)
      );
      toast.success(newStatus ? 'تم تفعيل المحفظة بنجاح.' : 'تم إيقاف تفعيل المحفظة.');
    } catch (err) {
      console.error(err);
      toast.error('فشل في تغيير حالة تفعيل المحفظة.');
    } finally {
      setActionLoading(null);
    }
  };

  const handleRegenerateToken = async (walletId: string) => {
    if (!confirm('هل أنت متأكد من إعادة توليد كود الربط؟ سيؤدي هذا إلى فصل التطبيق الحالي المرتبط بهذه المحفظة.')) {
      return;
    }
    setActionLoading(walletId);
    try {
      const response = await walletService.regenerateToken(walletId);
      if (response.success) {
        setWallets(prev => 
          prev.map(w => w.id === walletId ? { ...w, pairingToken: response.data, deviceStatus: 'Disconnected' } : w)
        );
        toast.success('تم إعادة توليد كود الربط بنجاح.');
      } else {
        toast.error(response.message || 'فشل في إعادة توليد كود الربط.');
      }
    } catch (err) {
      console.error(err);
      toast.error('فشل في إعادة توليد كود الربط.');
    } finally {
      setActionLoading(null);
    }
  };

  const handleOpenAddModal = () => {
    setPhoneNumber('');
    setLabel('');
    setDailyLimit(30000);
    setMonthlyLimit(100000);
    setSmsSenderFiltersText('VodafoneCash, EtisalatCash, OrangeCash');
    setActiveModal('add');
  };

  const handleOpenEditModal = (wallet: WalletDto) => {
    setSelectedWallet(wallet);
    setLabel(wallet.label);
    setDailyLimit(wallet.dailyLimit);
    setMonthlyLimit(wallet.monthlyLimit);
    setSmsSenderFiltersText(wallet.smsSenderFilters.join(', '));
    setActiveModal('edit');
  };

  const handleAddSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!phoneNumber.trim() || !label.trim()) {
      toast.error('يرجى ملء جميع الحقول المطلوبة.');
      return;
    }

    const filters = smsSenderFiltersText
      .split(/[,،]/)
      .map(s => s.trim())
      .filter(Boolean);

    const dto: CreateWalletDto = {
      phoneNumber: phoneNumber.trim(),
      label: label.trim(),
      dailyLimit,
      monthlyLimit,
      smsSenderFilters: filters,
    };

    try {
      setLoading(true);
      const response = await walletService.createWallet(dto);
      if (response.success && response.data) {
        toast.success('تم إضافة المحفظة الرقمية بنجاح.');
        setActiveModal(null);
        fetchWallets();
      } else {
        toast.error('فشل في إضافة المحفظة.');
      }
    } catch (err: any) {
      console.error(err);
      toast.error(err.response?.data?.message || 'فشل في إضافة المحفظة.');
    } finally {
      setLoading(false);
    }
  };

  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedWallet) return;

    if (!label.trim()) {
      toast.error('الاسم التعريفي للمحفظة مطلوب.');
      return;
    }

    const filters = smsSenderFiltersText
      .split(/[,،]/)
      .map(s => s.trim())
      .filter(Boolean);

    const dto: UpdateWalletLimitsDto = {
      label: label.trim(),
      dailyLimit,
      monthlyLimit,
      smsSenderFilters: filters,
    };

    try {
      setLoading(true);
      const response = await walletService.updateLimits(selectedWallet.id, dto);
      if (response.success) {
        toast.success('تم تحديث إعدادات المحفظة بنجاح.');
        setActiveModal(null);
        fetchWallets();
      } else {
        toast.error('فشل في تحديث المحفظة.');
      }
    } catch (err: any) {
      console.error(err);
      toast.error(err.response?.data?.message || 'فشل في تحديث المحفظة.');
    } finally {
      setLoading(false);
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    toast.success('تم نسخ كود الربط إلى الحافظة.');
  };

  // Calculations for stats
  const totalWallets = wallets.length;
  const activeWallets = wallets.filter(w => w.isActive).length;
  const connectedDevices = wallets.filter(w => w.deviceStatus === 'Connected').length;
  const totalBalance = wallets.reduce((acc, w) => acc + w.currentBalance, 0);

  const columns: AdminColumn<WalletDto>[] = [
    {
      key: 'walletInfo',
      label: 'المحفظة',
      render: (w) => (
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] shadow-[inset_0px_-1px_0px_0px_var(--admin-border)]">
            <Smartphone className="h-5 w-5" />
          </div>
          <div>
            <div className="font-bold text-[var(--admin-text)] text-sm">{w.label}</div>
            <div className="text-xs text-[var(--admin-muted)] font-mono mt-0.5">{w.phoneNumber}</div>
          </div>
        </div>
      )
    },
    {
      key: 'deviceStatus',
      label: 'اتصال الجهاز',
      render: (w) => {
        const isConnected = w.deviceStatus === 'Connected';
        return (
          <div className="flex flex-col items-start gap-1">
            <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold ${
              isConnected 
                ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-500' 
                : 'bg-rose-500/10 text-rose-600 dark:text-rose-500'
            }`}>
              {isConnected ? (
                <>
                  <Wifi className="h-3 w-3 animate-pulse" /> متصل
                </>
              ) : (
                <>
                  <WifiOff className="h-3 w-3" /> غير متصل
                </>
              )}
            </span>
            {w.lastSeenAt && (
              <span className="text-[10px] text-[var(--admin-muted)]">
                نشط {formatRelativeDate(w.lastSeenAt)}
              </span>
            )}
          </div>
        );
      }
    },
    {
      key: 'dailyReceived',
      label: 'الحد اليومي',
      render: (w) => {
        const ratio = w.dailyLimit > 0 ? (w.dailyReceived / w.dailyLimit) * 100 : 0;
        const isNearLimit = ratio >= 90;
        return (
          <div className="w-40 flex flex-col gap-1.5">
            <div className="flex justify-between text-xs font-semibold">
              <span className="text-[var(--admin-text)] font-mono">{w.dailyReceived} ج.م</span>
              <span className="text-[var(--admin-muted)] font-mono">/ {w.dailyLimit} ج.م</span>
            </div>
            <div className="h-2 w-full rounded-full bg-[var(--admin-card-strong)] overflow-hidden">
              <div 
                className={`h-full rounded-full transition-all duration-500 ${
                  isNearLimit ? 'bg-rose-500' : 'bg-[var(--admin-primary)]'
                }`}
                style={{ width: `${Math.min(100, ratio)}%` }}
              />
            </div>
          </div>
        );
      }
    },
    {
      key: 'monthlyReceived',
      label: 'الحد الشهري',
      render: (w) => {
        const ratio = w.monthlyLimit > 0 ? (w.monthlyReceived / w.monthlyLimit) * 100 : 0;
        const isNearLimit = ratio >= 90;
        return (
          <div className="w-40 flex flex-col gap-1.5">
            <div className="flex justify-between text-xs font-semibold">
              <span className="text-[var(--admin-text)] font-mono">{w.monthlyReceived} ج.م</span>
              <span className="text-[var(--admin-muted)] font-mono">/ {w.monthlyLimit} ج.م</span>
            </div>
            <div className="h-2 w-full rounded-full bg-[var(--admin-card-strong)] overflow-hidden">
              <div 
                className={`h-full rounded-full transition-all duration-500 ${
                  isNearLimit ? 'bg-rose-500' : 'bg-[var(--admin-primary)]'
                }`}
                style={{ width: `${Math.min(100, ratio)}%` }}
              />
            </div>
          </div>
        );
      }
    },
    {
      key: 'currentBalance',
      label: 'الرصيد الحالي',
      render: (w) => (
        <span className="font-mono font-bold text-sm text-[var(--admin-text)]">
          {w.currentBalance.toLocaleString('en-US')} ج.م
        </span>
      )
    },
    {
      key: 'pairingToken',
      label: 'كود الربط',
      render: (w) => (
        <div className="flex items-center gap-1.5">
          <code className="bg-[var(--admin-card-strong)] border border-[var(--admin-border)] px-2.5 py-1 rounded-lg text-xs font-mono font-bold text-[var(--admin-primary)] tracking-wider">
            {w.pairingToken}
          </code>
          <button 
            type="button"
            onClick={() => copyToClipboard(w.pairingToken)}
            title="نسخ الكود"
            className="p-1.5 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] transition-colors"
          >
            <Copy className="h-3.5 w-3.5" />
          </button>
          <button 
            type="button"
            onClick={() => handleRegenerateToken(w.id)}
            disabled={actionLoading === w.id}
            title="إعادة توليد كود الربط"
            className="p-1.5 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:text-amber-500 hover:bg-[var(--admin-hover)] transition-colors disabled:opacity-50"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${actionLoading === w.id ? 'animate-spin' : ''}`} />
          </button>
        </div>
      )
    },
    {
      key: 'isActive',
      label: 'التفعيل',
      render: (w) => (
        <div className="flex items-center justify-center">
          <label className="relative inline-flex items-center cursor-pointer select-none">
            <input 
              type="checkbox" 
              className="sr-only peer"
              checked={w.isActive}
              disabled={actionLoading === w.id}
              onChange={() => handleToggleActive(w.id, w.isActive)}
            />
            <div className="w-11 h-6 bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-full peer peer-focus:ring-2 peer-focus:ring-[var(--admin-primary-15)] peer-checked:after:translate-x-full rtl:peer-checked:after:-translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-0.5 after:start-[2px] after:bg-[var(--admin-muted)] peer-checked:after:bg-[var(--admin-primary)] after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-[var(--admin-primary-15)] peer-checked:border-[var(--admin-primary)] disabled:opacity-50"></div>
          </label>
        </div>
      )
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (w) => (
        <NeumorphButton
          type="button"
          onClick={() => handleOpenEditModal(w)}
          intent="ghost"
          size="sm"
        >
          <Edit2 className="h-3.5 w-3.5" />
          تعديل
        </NeumorphButton>
      )
    }
  ];

  return (
    <AdminShellChrome
      activePath="/admin/finance"
      sectionLabel="المالية والمدفوعات"
      pageTitle="إدارة المحافظ الرقمية"
      subtitle="إدارة محافظ Vodafone Cash وتطبيقات الاستماع والحدود اليومية للمطابقة الآلية."
      action={
        <NeumorphButton onClick={handleOpenAddModal} intent="primary" size="md">
          <Plus className="h-4 w-4" />
          إضافة محفظة جديدة
        </NeumorphButton>
      }
    >
      <div className="flex flex-col gap-6">
        {/* Stats */}
        {loading && wallets.length === 0 ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {[1, 2, 3, 4].map(i => (
              <div key={i} className="h-28 animate-pulse rounded-2xl bg-[var(--admin-card)] border border-[var(--admin-border)]" />
            ))}
          </div>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <AdminStatCard
              variant="light"
              icon={Smartphone}
              label="إجمالي المحافظ"
              value={totalWallets}
              subtitle="المحافظ المسجلة بالنظام"
            />
            <AdminStatCard
              variant="light"
              icon={Activity}
              label="المحافظ النشطة"
              value={activeWallets}
              subtitle="محافظ مفعلة لاستقبل طلبات الطلاب"
            />
            <AdminStatCard
              variant="light"
              icon={Wifi}
              label="الأجهزة المتصلة"
              value={connectedDevices}
              subtitle="أجهزة أندرويد متصلة حالياً"
            />
            <AdminStatCard
              variant="accent"
              icon={TrendingUp}
              label="إجمالي الأرصدة"
              value={`${totalBalance.toLocaleString('en-US')} ج.م`}
              subtitle="الرصيد الكلي المسجل بالمحافظ"
            />
          </div>
        )}

        {/* Table/Content */}
        <div className="admin-panel rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:p-6 shadow-[0_4px_20px_var(--admin-shadow)]">
          <h2 className="text-xl font-black text-[var(--admin-text)] mb-4">قائمة المحافظ المتصلة</h2>
          
          <AdminDataTable
            data={wallets}
            columns={columns}
            loading={loading}
            rowKey={(item) => item.id}
            emptyMessage="لا توجد محافظ رقمية مسجلة حالياً. اضغط على 'إضافة محفظة جديدة' للبدء."
            errorMessage={error}
            onRetry={fetchWallets}
          />
        </div>
      </div>

      {/* Add Wallet Modal */}
      <AdminModal
        open={activeModal === 'add'}
        onClose={() => setActiveModal(null)}
        title="إضافة محفظة رقمية جديدة"
        subtitle="أدخل تفاصيل المحفظة والحدود القصوى لاستقبال الأموال."
      >
        <form onSubmit={handleAddSubmit} className="mt-4 flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-bold text-[var(--admin-text)]">رقم الهاتف (الخاص بالمحفظة) *</label>
            <input 
              type="text" 
              required
              placeholder="مثال: 01012345678"
              value={phoneNumber}
              onChange={(e) => setPhoneNumber(e.target.value)}
              className="admin-input font-mono text-left direction-ltr"
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-bold text-[var(--admin-text)]">الاسم التعريفي للمحفظة *</label>
            <input 
              type="text" 
              required
              placeholder="مثال: محفظة فودافون كاش الرئيسية"
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              className="admin-input"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-bold text-[var(--admin-text)]">الحد اليومي (ج.م)</label>
              <input 
                type="number" 
                min="0"
                value={dailyLimit}
                onChange={(e) => setDailyLimit(Number(e.target.value))}
                className="admin-input font-mono"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-bold text-[var(--admin-text)]">الحد الشهري (ج.م)</label>
              <input 
                type="number" 
                min="0"
                value={monthlyLimit}
                onChange={(e) => setMonthlyLimit(Number(e.target.value))}
                className="admin-input font-mono"
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-bold text-[var(--admin-text)]">أسماء مرسلي الرسائل المستهدفة (SMS Senders)</label>
            <input 
              type="text" 
              value={smsSenderFiltersText}
              onChange={(e) => setSmsSenderFiltersText(e.target.value)}
              placeholder="مثال: VodafoneCash, InstaPay, OrangeCash"
              className="admin-input"
            />
            <span className="text-[11px] text-[var(--admin-muted)] leading-relaxed">
              اكتب أسماء الجهات التي تأتي منها رسائل التأكيد، مفصولة بفاصلة. سيقوم تطبيق الأندرويد بتصفية الرسائل بناءً على هذه القائمة فقط.
            </span>
          </div>

          <div className="mt-6 flex items-center justify-end gap-3">
            <NeumorphButton 
              type="button" 
              intent="ghost"
              onClick={() => setActiveModal(null)}
              disabled={loading}
            >
              إلغاء
            </NeumorphButton>
            <NeumorphButton 
              type="submit" 
              intent="primary"
              loading={loading}
            >
              حفظ وإضافة
            </NeumorphButton>
          </div>
        </form>
      </AdminModal>

      {/* Edit Wallet Modal */}
      <AdminModal
        open={activeModal === 'edit'}
        onClose={() => setActiveModal(null)}
        title="تعديل المحفظة الرقمية"
        subtitle={`تعديل تفاصيل وحدود المحفظة رقم: ${selectedWallet?.phoneNumber}`}
      >
        <form onSubmit={handleEditSubmit} className="mt-4 flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-bold text-[var(--admin-text)]">الاسم التعريفي للمحفظة *</label>
            <input 
              type="text" 
              required
              placeholder="مثال: محفظة فودافون كاش الرئيسية"
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              className="admin-input"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-bold text-[var(--admin-text)]">الحد اليومي (ج.م)</label>
              <input 
                type="number" 
                min="0"
                value={dailyLimit}
                onChange={(e) => setDailyLimit(Number(e.target.value))}
                className="admin-input font-mono"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-bold text-[var(--admin-text)]">الحد الشهري (ج.م)</label>
              <input 
                type="number" 
                min="0"
                value={monthlyLimit}
                onChange={(e) => setMonthlyLimit(Number(e.target.value))}
                className="admin-input font-mono"
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-bold text-[var(--admin-text)]">أسماء مرسلي الرسائل المستهدفة (SMS Senders)</label>
            <input 
              type="text" 
              value={smsSenderFiltersText}
              onChange={(e) => setSmsSenderFiltersText(e.target.value)}
              placeholder="مثال: VodafoneCash, InstaPay"
              className="admin-input"
            />
            <span className="text-[11px] text-[var(--admin-muted)] leading-relaxed">
              اكتب أسماء الجهات التي تأتي منها رسائل التأكيد، مفصولة بفاصلة. سيقوم تطبيق الأندرويد بتصفية الرسائل بناءً على هذه القائمة فقط.
            </span>
          </div>

          <div className="mt-6 flex items-center justify-end gap-3">
            <NeumorphButton 
              type="button" 
              intent="ghost"
              onClick={() => setActiveModal(null)}
              disabled={loading}
            >
              إلغاء
            </NeumorphButton>
            <NeumorphButton 
              type="submit" 
              intent="primary"
              loading={loading}
            >
              تحديث الإعدادات
            </NeumorphButton>
          </div>
        </form>
      </AdminModal>
    </AdminShellChrome>
  );
}

'use client';

import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  Save, 
  Info, 
  Plus, 
  Edit2, 
  Trash2, 
  X, 
  Activity, 
  Phone, 
  Globe, 
  Play, 
  MessageSquare,
  LockKeyhole,
  Check,
  AlertCircle
} from 'lucide-react';
import { AdminShellChrome, ConfirmDialog } from '@/components/admin';
import { adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';
import { devConsole } from '@/utils/dev-console';

interface RoleDto {
  id: string;
  name: string;
  type: string;
  permissions: string[];
}

const PERMISSION_DEFINITIONS = [
  { key: 'users.manage', label: 'إدارة الطلاب والمستخدمين', desc: 'تنشيط وتعليق حسابات الطلاب والمشرفين وإضافتهم' },
  { key: 'content.manage', label: 'إدارة المحتوى والمحاضرات', desc: 'إضافة وتعديل وحذف الباقات، الدروس، الفيديوهات، والملفات' },
  { key: 'exams.manage', label: 'إدارة الامتحانات والأسئلة', desc: 'إنشاء وتعديل الامتحانات وبنوك الأسئلة وتصحيح المقالي' },
  { key: 'codes.manage', label: 'إدارة الأكواد والاشتراكات', desc: 'توليد الأكواد شحن الرصيد والاشتراكات وإدارة مجموعات الأكواد' },
  { key: 'watch_requests.manage', label: 'إدارة طلبات إعادة المشاهدة', desc: 'الموافقة أو الرفض على طلبات زيادة مرات مشاهدة الفيديوهات' },
  { key: 'comments.manage', label: 'إدارة تعليقات الدروس', desc: 'الرد على أسئلة الطلاب على الدروس وحذف التعليقات غير اللائقة' },
  { key: 'community.manage', label: 'إدارة مجتمع الطلاب', desc: 'نشر منشورات عامة في مجتمع المنصة وإدارتها' },
  { key: 'settings.manage', label: 'إعدادات المنصة والأدوار', desc: 'التحكم باللوحة الفنية، الصيانة، الصلاحيات، وإعدادات الفيديو العامة' },
  { key: 'hr.manage', label: 'إدارة الموارد البشرية والموظفين', desc: 'تسجيل الحضور والانصراف، مراجعة الإجازات، والتحكم بملفات الموظفين' },
  { key: 'tasks.manage', label: 'إدارة العمليات والمهام المساعدة', desc: 'إسناد المهام للمساعدين، ومتابعة التنفيذ، واعتماد الطلبات التشغيلية' },
  { key: 'chat.manage', label: 'إدارة غرف المحادثات الداخلية', desc: 'التواصل الفوري بين الموظفين وفريق الإنتاج وإرسال الإشعارات والـ Mentions' },
  { key: 'crm.manage', label: 'إدارة علاقات الطلاب والمكالمات', desc: 'توزيع قوائم الطلاب على موظفي الكول سنتر، وتسجيل نتائج المكالمات والمتابعات' },
  { key: 'payments.manage', label: 'مطابقة شحن رصيد الطلاب تلقائياً', desc: 'استلام وقراءة رسائل الـ SMS، شحن أرصدة الطلاب، ومعالجة الحالات المعلقة' },
  { key: 'media.manage', label: 'إدارة خط إنتاج ونشر المحتوى', desc: 'متابعة مراحل تصوير ومونتاج ورفع المحاضرات وجدولة النشر على السوشيال ميديا' },
  { key: 'finance.manage', label: 'الحسابات المالية للمدرسين والموظفين', desc: 'حساب أرباح وعمولات المدرسين وأكواد التفعيل وصرف رواتب الموظفين' },
  { key: 'reports.manage', label: 'سجلات المراقبة والتقارير التشغيلية', desc: 'عرض الـ Audit logs، وإحصائيات أداء الموظفين، والمؤشرات العامة للمنصة' }
];

export default function AdminSettingsPageClient() {
  const [activeTab, setActiveTab] = useState<'settings' | 'roles'>('settings');
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  
  // Settings States
  const [settings, setSettings] = useState<Record<string, string>>({
    VideoWatchThresholdPercentage: '30',
    MaxExtraWatchRequestsPerVideo: '1',
    HintPenaltyPercentage: '20',
    PlatformName: 'منصة مسار',
    SupportPhoneNumber: '',
    SupportWhatsAppUrl: '',
    YouTubeChannelUrl: '',
    TelegramChannelUrl: '',
    MaxActiveDevicesPerStudent: '2',
    EnableWatermark: 'false',
    WatermarkOpacity: '0.15',
    MaintenanceMode: 'false',
    MaintenanceMessage: 'المنصة في أعمال صيانة مجدولة. سنعود قريباً!',
    BunnyStreamStorageRateUsdPerGb: '0.01',
    BunnyStreamBandwidthRateUsdPerGb: '0.005'
  });

  // Roles States
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [isRoleModalOpen, setIsRoleModalOpen] = useState(false);
  const [currentRole, setCurrentRole] = useState<RoleDto | null>(null);
  const [roleName, setRoleName] = useState('');
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([]);
  const [roleToDelete, setRoleToDelete] = useState<RoleDto | null>(null);

  const loadData = useCallback(async () => {
    setIsLoading(true);
    try {
      const settingsData = await adminService.getPlatformSettings();
      if (settingsData && Array.isArray(settingsData)) {
        const settingsRecord: Record<string, string> = {};
        settingsData.forEach((s: any) => {
          settingsRecord[s.key] = s.value;
        });
        setSettings(prev => ({ ...prev, ...settingsRecord }));
      }

      const rolesData = await adminService.listRoles();
      if (rolesData) {
        setRoles(rolesData);
      }
    } catch (err) {
      devConsole.error(err);
      toast.error('حدث خطأ أثناء تحميل البيانات');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    let isMounted = true;
    loadData().finally(() => { if (!isMounted) return; });
    return () => { isMounted = false; };
  }, [loadData]);

  const handleSettingChange = (key: string, value: string) => {
    setSettings(prev => ({ ...prev, [key]: value }));
  };

  const handleSaveSettings = async () => {
    setIsSaving(true);
    try {
      await adminService.updatePlatformSettings(settings);
      toast.success('تم حفظ إعدادات المنصة بنجاح ✅');
    } catch (err) {
      devConsole.error(err);
      toast.error('فشل في حفظ إعدادات المنصة');
    } finally {
      setIsSaving(false);
    }
  };

  const handleOpenRoleModal = (role: RoleDto | null = null) => {
    if (role) {
      setCurrentRole(role);
      setRoleName(role.name);
      setSelectedPermissions(role.permissions);
    } else {
      setCurrentRole(null);
      setRoleName('');
      setSelectedPermissions([]);
    }
    setIsRoleModalOpen(true);
  };

  const handleSaveRole = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!roleName.trim()) {
      toast.error('يرجى إدخال اسم الدور');
      return;
    }

    try {
      if (currentRole) {
        // Edit Role
        await adminService.updateRole(currentRole.id, {
          name: roleName.trim(),
          permissions: selectedPermissions
        });
        toast.success(`تم تحديث دور "${roleName}" بنجاح ✅`);
      } else {
        // Create Role
        await adminService.createRole({
          name: roleName.trim(),
          permissions: selectedPermissions
        });
        toast.success(`تم إنشاء دور "${roleName}" بنجاح ✅`);
      }
      setIsRoleModalOpen(false);
      const rolesData = await adminService.listRoles();
      if (rolesData) setRoles(rolesData);
    } catch (err) {
      devConsole.error(err);
      toast.error('حدث خطأ أثناء حفظ الدور');
    }
  };

  const handleDeleteRole = async () => {
    if (!roleToDelete) return;
    try {
      await adminService.deleteRole(roleToDelete.id);
      toast.success(`تم حذف الدور "${roleToDelete.name}" بنجاح`);
      setRoleToDelete(null);
      const rolesData = await adminService.listRoles();
      if (rolesData) setRoles(rolesData);
    } catch (err) {
      devConsole.error(err);
      toast.error('فشل في حذف الدور');
    }
  };

  const togglePermission = (permKey: string) => {
    setSelectedPermissions(prev => 
      prev.includes(permKey) ? prev.filter(p => p !== permKey) : [...prev, permKey]
    );
  };

  return (
    <AdminShellChrome
      activePath="/admin"
      sectionLabel="الإعدادات"
      pageTitle="الإعدادات والصلاحيات"
      subtitle="تخصيص عام للمنصة وإدارة أدوار المساعدين وصلاحياتهم الفنية."
    >
      {/* Dynamic Tab Switcher */}
      <div className="mb-8 flex justify-center">
        <div className="inline-flex gap-1 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-1.5 shadow-sm backdrop-blur-xl">
          <button
            onClick={() => setActiveTab('settings')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition ${activeTab === 'settings'
              ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
              : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            إعدادات المنصة
          </button>
          <button
            onClick={() => setActiveTab('roles')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition ${activeTab === 'roles'
              ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
              : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            الأدوار والصلاحيات
          </button>
        </div>
      </div>

      {isLoading ? (
        <div className="h-64 flex items-center justify-center">
          <div className="w-8 h-8 border-4 border-[var(--admin-primary)] border-t-transparent rounded-full animate-spin"></div>
        </div>
      ) : (
        <div className="max-w-4xl mx-auto space-y-6">
          <AnimatePresence mode="wait">
            {activeTab === 'settings' ? (
              <motion.div
                key="settings-tab"
                initial={{ opacity: 0, y: 15 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -15 }}
                transition={{ duration: 0.2 }}
                className="space-y-6"
                dir="rtl"
              >
                {/* Panel 1: General & Communication */}
                <div className="bg-[var(--admin-card)] rounded-3xl border border-[var(--admin-border)] shadow-[0_4px_30px_var(--admin-shadow)] p-6 sm:p-8 space-y-6">
                  <h2 className="text-lg font-black text-[var(--admin-text)] flex items-center gap-2 text-right">
                    <Globe className="w-5 h-5 text-[var(--admin-primary)]" />
                    <span>إعدادات عامة وتواصل الدعم</span>
                  </h2>
                  <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
                    <div className="space-y-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">اسم المنصة</label>
                      <input
                        type="text"
                        value={settings.PlatformName}
                        onChange={(e) => handleSettingChange('PlatformName', e.target.value)}
                        className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 px-4 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)]"
                        placeholder="مثال: منصة نادير جورج"
                      />
                    </div>
                    <div className="space-y-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">هاتف الدعم الفني</label>
                      <div className="relative">
                        <input
                          type="tel"
                          value={settings.SupportPhoneNumber}
                          onChange={(e) => handleSettingChange('SupportPhoneNumber', e.target.value)}
                          className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 pl-4 pr-10 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] text-left font-mono"
                          placeholder="01xxxxxxxxx"
                          dir="ltr"
                        />
                        <Phone className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--admin-muted)]" />
                      </div>
                    </div>
                    <div className="space-y-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">رابط واتساب الدعم</label>
                      <div className="relative">
                        <input
                          type="url"
                          value={settings.SupportWhatsAppUrl}
                          onChange={(e) => handleSettingChange('SupportWhatsAppUrl', e.target.value)}
                          className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 pl-4 pr-10 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] text-left font-mono"
                          placeholder="https://wa.me/..."
                          dir="ltr"
                        />
                        <MessageSquare className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--admin-muted)]" />
                      </div>
                    </div>
                    <div className="space-y-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">قناة اليوتيوب</label>
                      <div className="relative">
                        <input
                          type="url"
                          value={settings.YouTubeChannelUrl}
                          onChange={(e) => handleSettingChange('YouTubeChannelUrl', e.target.value)}
                          className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 pl-4 pr-10 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] text-left font-mono"
                          placeholder="https://youtube.com/..."
                          dir="ltr"
                        />
                        <Play className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--admin-muted)]" />
                      </div>
                    </div>
                    <div className="space-y-2 sm:col-span-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">قناة التليجرام</label>
                      <div className="relative">
                        <input
                          type="url"
                          value={settings.TelegramChannelUrl}
                          onChange={(e) => handleSettingChange('TelegramChannelUrl', e.target.value)}
                          className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 pl-4 pr-10 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] text-left font-mono"
                          placeholder="https://t.me/..."
                          dir="ltr"
                        />
                        <Globe className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--admin-muted)]" />
                      </div>
                    </div>
                  </div>
                </div>

                {/* Panel 2: Video Settings & Device Limits */}
                <div className="bg-[var(--admin-card)] rounded-3xl border border-[var(--admin-border)] shadow-[0_4px_30px_var(--admin-shadow)] p-6 sm:p-8 space-y-6">
                  <h2 className="text-lg font-black text-[var(--admin-text)] flex items-center gap-2 text-right">
                    <LockKeyhole className="w-5 h-5 text-[var(--admin-primary)]" />
                    <span>الحماية وإعدادات الفيديو</span>
                  </h2>
                  <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
                    <div className="space-y-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">نسبة مشاهدة الفيديو لاحتسابه</label>
                      <div className="relative">
                        <input
                          type="number"
                          min="1"
                          max="100"
                          value={settings.VideoWatchThresholdPercentage}
                          onChange={(e) => handleSettingChange('VideoWatchThresholdPercentage', e.target.value)}
                          className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 pl-10 pr-4 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] font-mono text-left"
                          dir="ltr"
                        />
                        <span className="absolute left-4 top-1/2 -translate-y-1/2 text-[var(--admin-muted)]">%</span>
                      </div>
                      <p className="text-xs text-[var(--admin-muted)]">النسبة التي يتم بعدها قفل أو إنقاص رصيد المحاضرة للطالب.</p>
                    </div>

                    <div className="space-y-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">الحد الأقصى لأجهزة الطالب النشطة</label>
                      <input
                        type="number"
                        min="1"
                        value={settings.MaxActiveDevicesPerStudent}
                        onChange={(e) => handleSettingChange('MaxActiveDevicesPerStudent', e.target.value)}
                        className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 px-4 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] font-mono text-left"
                        dir="ltr"
                      />
                      <p className="text-xs text-[var(--admin-muted)]">الحد الأقصى لعدد الأجهزة المسجلة للطالب الواحد لمنع تشارك الحسابات.</p>
                    </div>

                    <div className="space-y-3 sm:col-span-2 bg-[var(--admin-card-soft)] p-4 rounded-2xl border border-[var(--admin-border)] text-right">
                      <div className="flex items-center justify-between">
                        <button
                          type="button"
                          onClick={() => handleSettingChange('EnableWatermark', settings.EnableWatermark === 'true' ? 'false' : 'true')}
                          className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
                            settings.EnableWatermark === 'true' ? 'bg-[var(--admin-primary)]' : 'bg-[var(--admin-border)] dark:bg-[var(--admin-card-strong)]'
                          }`}
                        >
                          <span
                            className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                              settings.EnableWatermark === 'true' ? '-translate-x-5' : 'translate-x-0'
                            }`}
                          />
                        </button>
                        <div>
                          <label className="block text-sm font-bold text-[var(--admin-text)]">تفعيل العلامة المائية</label>
                          <span className="text-xs text-[var(--admin-muted)]">إظهار اسم ورقم هاتف الطالب متحركاً على مشغل الفيديو لمنع التسريب.</span>
                        </div>
                      </div>

                      {settings.EnableWatermark === 'true' && (
                        <div className="mt-4 space-y-2 pt-3 border-t border-[var(--admin-border)]">
                          <div className="flex justify-between text-xs text-[var(--admin-text)] font-semibold">
                            <span className="font-mono">{Math.round(parseFloat(settings.WatermarkOpacity) * 100)}%</span>
                            <span>شفافية العلامة المائية</span>
                          </div>
                          <input
                            type="range"
                            min="0.05"
                            max="1.0"
                            step="0.05"
                            value={settings.WatermarkOpacity}
                            onChange={(e) => handleSettingChange('WatermarkOpacity', e.target.value)}
                            className="w-full accent-[var(--admin-primary)]"
                          />
                        </div>
                      )}
                    </div>

                    <div className="space-y-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">سعر تخزين Bunny لكل GB</label>
                      <div className="relative">
                        <input
                          type="number"
                          min="0"
                          step="0.000001"
                          value={settings.BunnyStreamStorageRateUsdPerGb}
                          onChange={(e) => handleSettingChange('BunnyStreamStorageRateUsdPerGb', e.target.value)}
                          className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 pl-10 pr-4 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] font-mono text-left"
                          dir="ltr"
                        />
                        <span className="absolute left-4 top-1/2 -translate-y-1/2 text-[var(--admin-muted)]">$</span>
                      </div>
                      <p className="text-xs text-[var(--admin-muted)]">القيمة الافتراضية من Bunny Stream: 0.01 دولار لكل GB.</p>
                    </div>

                    <div className="space-y-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">سعر باندويث Bunny لكل GB</label>
                      <div className="relative">
                        <input
                          type="number"
                          min="0"
                          step="0.000001"
                          value={settings.BunnyStreamBandwidthRateUsdPerGb}
                          onChange={(e) => handleSettingChange('BunnyStreamBandwidthRateUsdPerGb', e.target.value)}
                          className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 pl-10 pr-4 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] font-mono text-left"
                          dir="ltr"
                        />
                        <span className="absolute left-4 top-1/2 -translate-y-1/2 text-[var(--admin-muted)]">$</span>
                      </div>
                      <p className="text-xs text-[var(--admin-muted)]">القيمة الافتراضية من Bunny Stream: 0.005 دولار لكل GB.</p>
                    </div>
                  </div>
                </div>

                {/* Panel 3: Watch Requests & Hint Penalty */}
                <div className="bg-[var(--admin-card)] rounded-3xl border border-[var(--admin-border)] shadow-[0_4px_30px_var(--admin-shadow)] p-6 sm:p-8 space-y-6">
                  <h2 className="text-lg font-black text-[var(--admin-text)] flex items-center gap-2 text-right">
                    <Info className="w-5 h-5 text-[var(--admin-primary)]" />
                    <span>إعدادات الطلبات والمكافآت</span>
                  </h2>
                  <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
                    <div className="space-y-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">أقصى طلبات زيادة مشاهدة إضافية</label>
                      <input
                        type="number"
                        min="0"
                        value={settings.MaxExtraWatchRequestsPerVideo}
                        onChange={(e) => handleSettingChange('MaxExtraWatchRequestsPerVideo', e.target.value)}
                        className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 px-4 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] font-mono text-left"
                        dir="ltr"
                      />
                      <p className="text-xs text-[var(--admin-muted)]">الحد الأقصى للمرات التي يمكن للطالب طلب مشاهدة إضافية للفيديو الواحد.</p>
                    </div>

                    <div className="space-y-2 text-right">
                      <label className="block text-sm font-bold text-[var(--admin-text)]">نسبة عقوبة استخدام التلميحات</label>
                      <div className="relative">
                        <input
                          type="number"
                          min="0"
                          max="100"
                          value={settings.HintPenaltyPercentage}
                          onChange={(e) => handleSettingChange('HintPenaltyPercentage', e.target.value)}
                          className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 pl-10 pr-4 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] font-mono text-left"
                          dir="ltr"
                        />
                        <span className="absolute left-4 top-1/2 -translate-y-1/2 text-[var(--admin-muted)]">%</span>
                      </div>
                      <p className="text-xs text-[var(--admin-muted)]">نسبة خصم النقاط من درجة السؤال عند عرض التلميح للطالب.</p>
                    </div>
                  </div>
                </div>

                {/* Panel 4: Maintenance Mode */}
                <div className="bg-[var(--admin-card)] rounded-3xl border border-[var(--admin-border)] shadow-[0_4px_30px_var(--admin-shadow)] p-6 sm:p-8 space-y-6">
                  <div className="flex items-center justify-between">
                    <button
                      type="button"
                      onClick={() => handleSettingChange('MaintenanceMode', settings.MaintenanceMode === 'true' ? 'false' : 'true')}
                      className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
                        settings.MaintenanceMode === 'true' ? 'bg-amber-500' : 'bg-[var(--admin-border)] dark:bg-[var(--admin-card-strong)]'
                      }`}
                    >
                      <span
                        className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                          settings.MaintenanceMode === 'true' ? '-translate-x-5' : 'translate-x-0'
                        }`}
                      />
                    </button>
                    <h2 className="text-lg font-black text-[var(--admin-text)] flex items-center gap-2">
                      <Activity className="w-5 h-5 text-amber-500" />
                      <span>وضع الصيانة</span>
                    </h2>
                  </div>
                  
                  {settings.MaintenanceMode === 'true' && (
                    <motion.div
                      initial={{ opacity: 0, height: 0 }}
                      animate={{ opacity: 1, height: 'auto' }}
                      className="space-y-4 pt-4 border-t border-[var(--admin-border)]"
                    >
                      <div className="flex items-center gap-2 rounded-xl bg-amber-50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-900/30 p-4 text-amber-800 dark:text-amber-300 text-sm text-right">
                        <AlertCircle className="w-5 h-5 shrink-0" />
                        <span>عند تفعيل وضع الصيانة، سيتم توجيه جميع الطلاب تلقائياً إلى صفحة الصيانة الفورية بشكل كامل ومنع وصولهم لأي محتوى دراسي.</span>
                      </div>
                      
                      <div className="space-y-2 text-right">
                        <label className="block text-sm font-bold text-[var(--admin-text)]">رسالة وضع الصيانة للطلاب</label>
                        <textarea
                          rows={3}
                          value={settings.MaintenanceMessage}
                          onChange={(e) => handleSettingChange('MaintenanceMessage', e.target.value)}
                          className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 px-4 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] resize-none"
                          placeholder="اكتب هنا الرسالة التي تظهر للطالب في شاشة الصيانة..."
                        />
                      </div>
                    </motion.div>
                  )}
                </div>

                {/* Save Button */}
                <div className="flex justify-end pt-4">
                  <button
                    onClick={handleSaveSettings}
                    disabled={isSaving}
                    className="flex items-center gap-2 px-8 py-3.5 bg-[var(--admin-primary)] text-white font-bold rounded-2xl hover:bg-[var(--admin-primary-strong)] transition-all shadow-[0_4px_15px_var(--admin-shadow)] disabled:opacity-50"
                  >
                    {isSaving ? (
                      <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                    ) : (
                      <Save className="w-5 h-5" />
                    )}
                    <span>{isSaving ? 'جاري حفظ التغييرات...' : 'حفظ الإعدادات الفنية'}</span>
                  </button>
                </div>
              </motion.div>
            ) : (
              <motion.div
                key="roles-tab"
                initial={{ opacity: 0, y: 15 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -15 }}
                transition={{ duration: 0.2 }}
                className="space-y-6"
                dir="rtl"
              >
                {/* Header Action */}
                <div className="flex justify-between items-center text-right">
                  <div>
                    <h3 className="text-xl font-black text-[var(--admin-text)]">الأدوار المخصصة للمشرفين</h3>
                    <p className="text-xs text-[var(--admin-muted)]">قم بإنشاء أدوار فنية مخصصة بمجموعة فرعية من الصلاحيات للمساعدين.</p>
                  </div>
                  <button
                    onClick={() => handleOpenRoleModal(null)}
                    className="flex items-center gap-2 px-5 py-2.5 bg-[var(--admin-primary)] text-white font-bold rounded-full hover:bg-[var(--admin-primary-strong)] transition-all shadow-[0_4px_15px_var(--admin-shadow)]"
                  >
                    <Plus className="w-4 h-4" />
                    <span>إضافة دور جديد</span>
                  </button>
                </div>

                {/* Roles Grid List */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-right">
                  {roles.map((role) => {
                    const isSystemRole = role.name === 'Admin' || role.name === 'Student' || role.name === 'Teacher';
                    return (
                      <div 
                        key={role.id}
                        className="bg-[var(--admin-card)] rounded-3xl border border-[var(--admin-border)] shadow-sm p-6 flex flex-col justify-between hover:shadow-md transition-shadow relative overflow-hidden"
                      >
                        <div className="space-y-4">
                          <div className="flex justify-between items-start">
                            {isSystemRole ? (
                              <span className="bg-[var(--admin-primary-15)] text-[var(--admin-primary)] text-xs font-bold px-2.5 py-1 rounded-full border border-[var(--admin-primary)]/15">
                                دور نظام أساسي
                              </span>
                            ) : (
                              <span className="bg-[var(--admin-card-strong)] text-[var(--admin-muted)] text-xs font-bold px-2.5 py-1 rounded-full">
                                دور مخصص
                              </span>
                            )}
                            <div>
                              <h4 className="text-base font-extrabold text-[var(--admin-text)]">{role.name}</h4>
                              <span className="text-xs text-[var(--admin-muted)]">النوع في قاعدة البيانات: {role.type}</span>
                            </div>
                          </div>

                          {/* Permissions Preview Tags */}
                          <div className="space-y-2">
                            <span className="text-xs font-bold text-[var(--admin-muted)]">الصلاحيات الممنوحة:</span>
                            {role.name === 'Admin' || role.name === 'Teacher' ? (
                              <div className="text-xs text-emerald-600 font-semibold bg-emerald-50 dark:bg-emerald-950/20 border border-emerald-150 p-2.5 rounded-xl">
                                وصول فوري وكامل لجميع الخصائص والمميزات (تخطي الصلاحيات)
                              </div>
                            ) : role.permissions.length === 0 ? (
                              <div className="text-xs text-[var(--admin-muted)] italic">لا توجد صلاحيات مخصصة مسجلة</div>
                            ) : (
                              <div className="flex flex-wrap gap-1.5 justify-end">
                                {role.permissions.map(p => {
                                  const def = PERMISSION_DEFINITIONS.find(d => d.key === p);
                                  return (
                                    <span key={p} className="bg-[var(--admin-card-strong)] text-[var(--admin-text)] text-xs font-medium px-2 py-1 rounded-lg border border-[var(--admin-border)] shadow-2xs">
                                      {def ? def.label : p}
                                    </span>
                                  );
                                })}
                              </div>
                            )}
                          </div>
                        </div>

                        {/* Actions */}
                        {!isSystemRole && (
                          <div className="flex justify-end gap-2 mt-6 pt-4 border-t border-[var(--admin-border)]">
                            <button
                              onClick={() => handleOpenRoleModal(role)}
                              className="p-2 text-[var(--admin-muted)] hover:text-[var(--admin-primary)] transition-colors rounded-lg bg-[var(--admin-bg)] border border-[var(--admin-border)]"
                              title="تعديل صلاحيات الدور"
                            >
                              <Edit2 className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => setRoleToDelete(role)}
                              className="p-2 text-red-500 hover:bg-red-50 dark:hover:bg-red-950/20 transition-colors rounded-lg bg-[var(--admin-bg)] border border-[var(--admin-border)]"
                              title="حذف الدور"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      )}

      {/* Role Add/Edit Modal (Glassmorphic Drawer) */}
      <AnimatePresence>
        {isRoleModalOpen && (
          <>
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="fixed inset-0 z-40 bg-black/40 backdrop-blur-xs"
              onClick={() => setIsRoleModalOpen(false)}
            />
            <motion.div
              initial={{ opacity: 0, scale: 0.95, y: 10 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 10 }}
              className="fixed inset-0 m-auto z-50 w-full max-w-2xl max-h-[85vh] overflow-y-auto rounded-3xl bg-[var(--admin-bg)] border border-[var(--admin-border)] shadow-2xl p-6 sm:p-8"
              dir="rtl"
            >
              <div className="flex justify-between items-center pb-4 mb-6 border-b border-[var(--admin-border)] text-right">
                <button
                  onClick={() => setIsRoleModalOpen(false)}
                  className="p-2 rounded-xl bg-[var(--admin-card-strong)] text-[var(--admin-muted)] hover:text-[var(--admin-text)] transition-colors border border-[var(--admin-border)]"
                >
                  <X className="w-4 h-4" />
                </button>
                <div>
                  <h3 className="text-lg font-black text-[var(--admin-text)]">
                    {currentRole ? `تعديل صلاحيات الدور: ${currentRole.name}` : 'إضافة دور فني جديد'}
                  </h3>
                  <p className="text-xs text-[var(--admin-muted)]">حدد اسم الدور وصلاحياته الفنية التي يحق له تنفيذها.</p>
                </div>
              </div>

              <form onSubmit={handleSaveRole} className="space-y-6 text-right">
                <div className="space-y-2">
                  <label className="block text-sm font-bold text-[var(--admin-text)]">اسم الدور</label>
                  <input
                    type="text"
                    required
                    value={roleName}
                    onChange={(e) => setRoleName(e.target.value)}
                    placeholder="مثال: مصحح لغة عربية، مساعد دعم"
                    className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 px-4 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] text-right"
                  />
                </div>

                <div className="space-y-3">
                  <label className="block text-sm font-bold text-[var(--admin-text)]">تحديد الصلاحيات الممنوحة</label>
                  <div className="space-y-2 max-h-[40vh] overflow-y-auto pr-1">
                    {PERMISSION_DEFINITIONS.map((perm) => {
                      const isChecked = selectedPermissions.includes(perm.key);
                      return (
                        <div 
                          key={perm.key}
                          onClick={() => togglePermission(perm.key)}
                          className={`flex items-start gap-3 p-3 rounded-2xl border transition-all cursor-pointer select-none text-right ${
                            isChecked 
                              ? 'bg-[var(--admin-primary-15)] border-[var(--admin-primary)] text-[var(--admin-primary)]' 
                              : 'bg-[var(--admin-card-soft)] border-[var(--admin-border)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'
                          }`}
                        >
                          <div className={`mt-0.5 w-5 h-5 rounded-md border flex items-center justify-center transition-colors shrink-0 ${
                            isChecked ? 'bg-[var(--admin-primary)] border-[var(--admin-primary)] text-white' : 'border-[var(--admin-border)] bg-[var(--admin-card)]'
                          }`}>
                            {isChecked && <Check className="w-3.5 h-3.5 stroke-[3]" />}
                          </div>
                          <div className="flex-1">
                            <span className="block text-sm font-bold">{perm.label}</span>
                            <span className="block text-xs opacity-75 mt-0.5">{perm.desc}</span>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>

                <div className="flex justify-end gap-3 pt-4 border-t border-[var(--admin-border)]">
                  <button
                    type="button"
                    onClick={() => setIsRoleModalOpen(false)}
                    className="px-6 py-3 border border-[var(--admin-border)] rounded-xl text-sm font-bold hover:bg-[var(--admin-hover)] text-[var(--admin-text)] transition-colors"
                  >
                    إلغاء
                  </button>
                  <button
                    type="submit"
                    className="px-6 py-3 bg-[var(--admin-primary)] hover:bg-[var(--admin-primary-strong)] text-white text-sm font-bold rounded-xl shadow-md transition-colors"
                  >
                    {currentRole ? 'تحديث الدور' : 'إنشاء الدور'}
                  </button>
                </div>
              </form>
            </motion.div>
          </>
        )}
      </AnimatePresence>

      {/* Delete Role Dialog */}
      <ConfirmDialog
        open={!!roleToDelete}
        title="حذف الدور المخصص؟"
        description={`هل أنت متأكد من رغبتك في حذف دور "${roleToDelete?.name}" نهائياً؟ هذا الإجراء غير قابل للتراجع وسيؤدي لإزالة كافة الصلاحيات الممنوحة للمشرفين التابعين له.`}
        confirmLabel="نعم، حذف الدور"
        cancelLabel="إلغاء"
        variant="danger"
        onConfirm={handleDeleteRole}
        onCancel={() => setRoleToDelete(null)}
      />
    </AdminShellChrome>
  );
}

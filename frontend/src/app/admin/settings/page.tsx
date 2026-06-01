'use client';

import { useState, useEffect } from 'react';
import { AdminShellChrome } from '@/components/admin/AdminShellChrome';
import { Save, Info } from 'lucide-react';
import apiClient from '@/services/api-client';

export default function AdminSettingsPage() {
  const [threshold, setThreshold] = useState<string>('30');
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [saveStatus, setSaveStatus] = useState<'idle' | 'success' | 'error'>('idle');

  useEffect(() => {
    apiClient.get('/admin/settings').then((res) => {
      const settings = res.data.data;
      const t = settings.find((s: any) => s.key === 'VideoWatchThresholdPercentage');
      if (t) setThreshold(t.value);
      setIsLoading(false);
    }).catch(err => {
      console.error(err);
      setIsLoading(false);
    });
  }, []);

  const handleSave = async () => {
    setIsSaving(true);
    setSaveStatus('idle');
    try {
      await apiClient.put('/admin/settings', {
        settings: {
          VideoWatchThresholdPercentage: threshold || '30'
        }
      });
      setSaveStatus('success');
      setTimeout(() => setSaveStatus('idle'), 3000);
    } catch (err) {
      console.error(err);
      setSaveStatus('error');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <AdminShellChrome
      activePath="/admin" // Assuming we don't have a direct nav item for settings right now
      sectionLabel="الإعدادات"
      pageTitle="إعدادات المنصة"
      subtitle="تخصيص الخصائص العامة للنظام"
    >
      <div className="max-w-xl bg-[var(--admin-card)] rounded-[26px] border border-[var(--admin-border)] shadow-[0_4px_30px_var(--admin-shadow)] p-6 sm:p-8">
        
        <h2 className="text-xl font-bold mb-6 text-[var(--admin-text)]">إعدادات مشاهدات الفيديو</h2>
        
        {isLoading ? (
          <div className="h-20 flex items-center justify-center">
            <div className="w-6 h-6 border-2 border-[var(--admin-primary)] border-t-transparent rounded-full animate-spin"></div>
          </div>
        ) : (
          <div className="space-y-6">
            <div className="space-y-3">
              <label className="block text-sm font-bold text-[var(--admin-text)]">نسبة اكتمال المشاهدة المطلوبة لاحتساب &quot;مشاهدة&quot; (Percentage %)</label>
              <div className="relative">
                <input 
                  type="number" 
                  min="1" 
                  max="100" 
                  value={threshold}
                  onChange={(e) => setThreshold(e.target.value)}
                  className="w-full bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-xl py-3 px-4 text-[var(--admin-text)] focus:outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] placeholder-gray-500 transition-all font-mono"
                  dir="ltr"
                />
                <span className="absolute left-4 top-1/2 -translate-y-1/2 text-[var(--admin-muted)]">%</span>
              </div>
              <p className="text-xs text-[var(--admin-muted)] flex items-start gap-1">
                <Info className="w-4 h-4 shrink-0" />
                <span>سيتم احتساب المشاهدة في رصيد الطالب كـ &quot;مُشاهدة مستهلكة&quot; وتحديث قفل الفيديو عند تجاوزه هذه النسبة من مدة الفيديو الإجمالية.</span>
              </p>
            </div>

            <div className="pt-4 border-t border-[var(--admin-border)] flex items-center justify-end gap-3">
              {saveStatus === 'success' && <span className="text-green-500 text-sm font-semibold">تم الحفظ بنجاح</span>}
              {saveStatus === 'error' && <span className="text-red-500 text-sm font-semibold">فشل الحفظ</span>}
              <button
                disabled={isSaving}
                onClick={handleSave}
                className="flex items-center gap-2 px-6 py-3 bg-[var(--admin-primary)] text-white font-bold rounded-xl hover:bg-[var(--admin-primary-strong)] transition-all shadow-[0_4px_15px_var(--admin-shadow)] disabled:opacity-50"
              >
                <Save className="w-5 h-5" />
                <span>{isSaving ? 'جاري الحفظ...' : 'حفظ الإعدادات'}</span>
              </button>
            </div>
          </div>
        )}
      </div>
    </AdminShellChrome>
  );
}

'use client';

import { Bell, BellRing, Settings2, Volume2 } from 'lucide-react';

import { playLiveSupportSound, type LiveSupportPreferences, type LiveSupportSound } from '@/hooks/useLiveSupportPreferences';

type StaffChatSettingsProps = {
  open: boolean;
  preferences: LiveSupportPreferences;
  onClose: () => void;
  onChange: (change: Partial<LiveSupportPreferences>) => void;
};

const sounds: Array<{ value: LiveSupportSound; label: string }> = [
  { value: 'soft', label: 'هادئ' },
  { value: 'bell', label: 'جرس' },
  { value: 'chime', label: 'نغمة' },
];

export function StaffChatSettings({ open, preferences, onClose, onChange }: StaffChatSettingsProps) {
  if (!open) return null;

  const setNotifications = async (enabled: boolean) => {
    if (enabled && typeof Notification !== 'undefined' && Notification.permission === 'default') {
      await Notification.requestPermission();
    }
    onChange({ notificationsEnabled: enabled });
  };

  return <section aria-labelledby="chat-settings-title" className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
    <div className="flex items-center justify-between gap-3">
      <div className="flex items-center gap-2"><Settings2 className="size-5 text-[var(--admin-primary)]"/><div><h2 id="chat-settings-title" className="font-bold text-[var(--admin-text)]">إعدادات محادثتي</h2><p className="text-xs text-[var(--admin-muted)]">تُحفظ تلقائيًا لهذا الحساب على هذا الجهاز.</p></div></div>
      <button type="button" onClick={onClose} className="min-h-11 rounded-lg px-3 text-sm font-semibold text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]">إغلاق</button>
    </div>
    <div className="mt-4 grid gap-4 lg:grid-cols-2">
      <fieldset><legend className="text-sm font-bold text-[var(--admin-text)]">ألوان الرسائل</legend><div className="mt-2 grid grid-cols-2 gap-2"><label className="rounded-xl border border-[var(--admin-border)] p-2 text-xs font-semibold text-[var(--admin-text)]">رسالتي<input aria-label="لون رسالة الموظف" type="color" value={preferences.staffBubbleColor} onChange={(event) => onChange({ staffBubbleColor: event.target.value })} className="mt-2 h-11 w-full cursor-pointer rounded"/></label><label className="rounded-xl border border-[var(--admin-border)] p-2 text-xs font-semibold text-[var(--admin-text)]">رسالة الطالب<input aria-label="لون رسالة الطالب" type="color" value={preferences.studentBubbleColor} onChange={(event) => onChange({ studentBubbleColor: event.target.value })} className="mt-2 h-11 w-full cursor-pointer rounded"/></label></div></fieldset>
      <fieldset><legend className="text-sm font-bold text-[var(--admin-text)]">حجم الخط</legend><div className="mt-2 flex gap-2">{([{ value: 'small', label: 'صغير' }, { value: 'medium', label: 'متوسط' }, { value: 'large', label: 'كبير' }] as const).map((option) => <button key={option.value} type="button" onClick={() => onChange({ fontScale: option.value })} className={`min-h-11 rounded-lg border px-3 text-sm font-semibold ${preferences.fontScale === option.value ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}>{option.label}</button>)}</div></fieldset>
      <fieldset><legend className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]"><BellRing className="size-4"/>تنبيه الرسائل</legend><label className="mt-2 flex min-h-11 items-center gap-2 text-sm text-[var(--admin-text)]"><input type="checkbox" checked={preferences.notificationsEnabled} onChange={(event) => void setNotifications(event.target.checked)} className="size-4 accent-[var(--admin-primary)]"/>إظهار إشعار عند وصول رسالة من الطالب</label><label className="mt-2 flex min-h-11 items-center gap-2 text-sm text-[var(--admin-text)]"><Bell className="size-4"/><input type="checkbox" checked={preferences.soundEnabled} onChange={(event) => onChange({ soundEnabled: event.target.checked })} className="size-4 accent-[var(--admin-primary)]"/>تشغيل صوت مع التنبيه</label></fieldset>
      <fieldset><legend className="flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]"><Volume2 className="size-4"/>نغمة التنبيه</legend><div className="mt-2 flex flex-wrap gap-2">{sounds.map((sound) => <button key={sound.value} type="button" onClick={() => { onChange({ sound: sound.value }); playLiveSupportSound(sound.value); }} className={`min-h-11 rounded-lg border px-3 text-sm font-semibold ${preferences.sound === sound.value ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}>{sound.label}</button>)}</div></fieldset>
    </div>
  </section>;
}

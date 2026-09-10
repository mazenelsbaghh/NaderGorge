'use client';

import type { CSSProperties } from 'react';

import { normalizeWatermarkSettings, watermarkLines, type WatermarkSettings } from '@/lib/video-watermark';

const fields = [
  ['المنصة', 'WatermarkShowBrand', 'WatermarkBrandColor'],
  ['اسم الطالب', 'WatermarkShowName', 'WatermarkNameColor'],
  ['رقم الهاتف', 'WatermarkShowPhone', 'WatermarkPhoneColor'],
  ['معرّف الطالب', 'WatermarkShowStudentId', 'WatermarkStudentIdColor'],
  ['نص إضافي', 'WatermarkShowCustom', 'WatermarkCustomColor'],
] as const;
const sliders = [
  ['الوضوح', 'WatermarkOpacity', 0.05, 1, 0.05],
  ['حجم الخط', 'WatermarkFontSize', 10, 36, 1],
  ['وضوح الخلفية', 'WatermarkBackgroundOpacity', 0, 1, 0.05],
  ['الفاصل بين الحركات بالثواني', 'WatermarkIntervalSeconds', 3, 120, 1],
] as const;

const previewPositions: Record<string, CSSProperties> = {
  'top-left': { insetBlockStart: 12, insetInlineEnd: 12 },
  'top-right': { insetBlockStart: 12, insetInlineStart: 12 },
  'bottom-left': { insetBlockEnd: 12, insetInlineEnd: 12 },
  'bottom-right': { insetBlockEnd: 12, insetInlineStart: 12 },
  center: { insetBlockStart: '50%', insetInlineStart: '50%', transform: 'translate(50%, -50%)' },
};

function hexToRgba(hex: string, opacity: number): string {
  const components = hex.slice(1).match(/.{2}/g)?.map(value => Number.parseInt(value, 16)) ?? [0, 0, 0];
  return `rgba(${components.join(',')},${opacity})`;
}

export function WatermarkSettingsEditor({ settings: raw, onChange }: {
  settings: Record<string, string>; onChange: (key: string, value: string) => void;
}) {
  const settings = normalizeWatermarkSettings(raw);
  const inputClass = 'min-h-11 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-[var(--admin-text)]';
  const lines = watermarkLines(settings, { name: 'أحمد محمد', phone: '01000000000', studentId: 'معرّف تجريبي' });
  const select = (label: string, key: keyof WatermarkSettings, options: readonly (readonly [string, string])[]) => (
    <label className="grid gap-2 text-sm font-semibold">{label}
      <select className={inputClass} value={settings[key]} onChange={event => onChange(key, event.target.value)}>
        {options.map(([value, text]) => <option key={value} value={value}>{text}</option>)}
      </select>
    </label>
  );
  return <section className="space-y-4 border-t border-[var(--admin-border)] pt-4" dir="rtl" aria-label="تخصيص العلامة المائية">
    <p className="text-sm text-[var(--admin-text)]">اختَر البيانات التي تظهر فوق الفيديو. التغييرات تُطبَّق عند فتح الفيديو بعد حفظ الإعدادات.</p>
    <div className="grid gap-4 md:grid-cols-2">
      <div className="space-y-2">
        {fields.map(([label, visibility, color]) => <div key={visibility} className="flex min-h-11 items-center justify-between gap-3">
          <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={settings[visibility] === 'true'} onChange={event => onChange(visibility, String(event.target.checked))} />{label}</label>
          <input type="color" className="h-11 w-14 cursor-pointer" aria-label={`لون ${label}`} value={settings[color]} onChange={event => onChange(color, event.target.value)} />
        </div>)}
        <label className="grid gap-2 text-sm">اسم المنصة<input className={inputClass} maxLength={120} value={settings.WatermarkBrandText} onChange={event => onChange('WatermarkBrandText', event.target.value)} /></label>
        <label className="grid gap-2 text-sm">النص الإضافي<input className={inputClass} maxLength={120} value={settings.WatermarkCustomText} onChange={event => onChange('WatermarkCustomText', event.target.value)} /></label>
      </div>
      <div className="space-y-3">
        {sliders.map(([label, key, min, max, step]) => <label key={key} className="grid gap-2 text-sm">{label}: {settings[key]}
          <input type="range" min={min} max={max} step={step} value={settings[key]} onChange={event => onChange(key, event.target.value)} />
        </label>)}
        {select('مكان العلامة', 'WatermarkPosition', [['top-left', 'أعلى اليسار'], ['top-right', 'أعلى اليمين'], ['bottom-left', 'أسفل اليسار'], ['bottom-right', 'أسفل اليمين'], ['center', 'المنتصف']])}
        {select('الخط', 'WatermarkFontFamily', [['Tajawal', 'تجوال'], ['Montserrat', 'Montserrat'], ['system-ui', 'خط الجهاز']])}
        {select('سُمك الخط', 'WatermarkFontWeight', [['400', 'عادي'], ['700', 'عريض'], ['900', 'عريض جدًا']])}
        <label className="flex min-h-11 items-center justify-between text-sm">لون الخلفية<input aria-label="لون خلفية العلامة" className="h-11 w-14" type="color" value={settings.WatermarkBackgroundColor} onChange={event => onChange('WatermarkBackgroundColor', event.target.value)} /></label>
        {([['تحريك العلامة', 'WatermarkMoving'], ['ظل النص', 'WatermarkTextShadow']] as const).map(([label, key]) => <label key={key} className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={settings[key] === 'true'} onChange={event => onChange(key, String(event.target.checked))} />{label}</label>)}
      </div>
    </div>
    <div className="relative min-h-56 overflow-hidden rounded-xl bg-[#0A1D3D] p-4" aria-label="معاينة العلامة المائية">
      <p className="text-center text-sm text-white/80">معاينة ببيانات تجريبية</p>
      <div className="absolute max-w-[70%] rounded px-2 py-1 text-center leading-snug" style={{
        ...previewPositions[settings.WatermarkPosition],
        opacity: Number(settings.WatermarkOpacity),
        fontSize: Number(settings.WatermarkFontSize),
        fontWeight: Number(settings.WatermarkFontWeight),
        fontFamily: settings.WatermarkFontFamily,
        textShadow: settings.WatermarkTextShadow === 'true' ? '0 1px 3px #000' : 'none',
        backgroundColor: hexToRgba(settings.WatermarkBackgroundColor, Number(settings.WatermarkBackgroundOpacity)),
      }}>
        {settings.EnableWatermark === 'true' ? lines.map((line, index) => <div key={index} style={{ color: line.color }}>{line.text}</div>) : <span className="text-white">العلامة المائية متوقفة</span>}
      </div>
    </div>
    <p className="text-sm text-[var(--admin-text)]">في ملء الشاشة الأصلي للآيفون تظهر أزرار الجهاز، ولا تظهر العلامة المائية المضافة فوق الفيديو.</p>
  </section>;
}

import { formatCairoTimestamp } from '@/lib/cairo-time';

export const roleLabel = (role: string) => role === 'host' ? 'الرئيسي' : 'الفرعي';
export const osLabel = (os: string) => ({ windows: 'ويندوز', 'windows-x64': 'ويندوز', macos: 'ماك', 'macos-arm64': 'ماك Apple Silicon' })[os] ?? os;
export function timestamp(value: string) {
  return Number.isNaN(Date.parse(value)) ? 'غير متاح' : formatCairoTimestamp(value);
}
export function sizeLabel(bytes: number) {
  return bytes < 1_048_576 ? `${(bytes / 1024).toFixed(1)} KB` : `${(bytes / 1_048_576).toFixed(1)} MB`;
}
export function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 30_000);
}
export function operationLabel(operation: string) {
  if (operation.startsWith('cloud.')) return 'المزامنة والتحديث';
  if (operation.startsWith('database.') || operation.startsWith('store_')) return 'حفظ البيانات';
  if (operation.startsWith('startup')) return 'فتح البرنامج';
  if (operation.includes('attendance')) return 'تسجيل الحضور';
  if (operation.startsWith('auth')) return 'تسجيل الدخول';
  if (operation.startsWith('lan.')) return 'ربط الأجهزة';
  if (operation.includes('print')) return 'الطباعة';
  return 'تشغيل البرنامج';
}

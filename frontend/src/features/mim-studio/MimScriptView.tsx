'use client';

import { Camera, Copy, Pencil, Check } from 'lucide-react';
import { useState } from 'react';
import toast from 'react-hot-toast';
import { generationPrompt, sceneScript, shotTime, type MimDocument, type MimShot } from './contract';

export async function copyStudioText(text: string) {
  try { await navigator.clipboard.writeText(text); toast.success('تم النسخ'); }
  catch { toast.error('تعذر النسخ. استخدم تنزيل الاسكربت.'); }
}

const inputStyle = 'min-h-11 w-full rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 text-sm leading-7 text-[var(--admin-text)] focus:outline-2 focus:outline-[var(--admin-primary)]';

export function MimScriptView({ document, selected, onChange, disabled = false }: {
  document: MimDocument; selected: number; onChange: (next: MimDocument) => void; disabled?: boolean;
}) {
  const [editing, setEditing] = useState(false);
  const scene = document.scenes[selected];
  const updateShot = (index: number, field: keyof MimShot, value: string) => onChange({
    ...document, scenes: document.scenes.map((item, sceneIndex) => sceneIndex === selected
      ? { ...item, shots: item.shots.map((shot, shotIndex) => shotIndex === index ? { ...shot, [field]: value } : shot) } : item),
  });
  return <article className="min-w-0" aria-labelledby="mim-scene-heading">
    <header className="flex flex-wrap items-start justify-between gap-4 border-b border-[var(--admin-border)] pb-5">
      <div>
        <p className="text-sm font-bold text-[var(--admin-primary)]">المشهد {selected + 1} · ٣٠ ثانية · {scene.shots.length} كادرات</p>
        <h3 id="mim-scene-heading" className="mt-2 text-2xl font-black leading-relaxed text-[var(--admin-text)]">{scene.title}</h3>
        <p className="mt-2 max-w-prose text-sm leading-7 text-[var(--admin-muted)]">{scene.educationalPoint}</p>
      </div>
      <div className="flex flex-wrap gap-2">
        <button type="button" className="admin-btn-ghost min-h-11" onClick={() => void copyStudioText(sceneScript(scene, selected))}><Copy className="h-4 w-4" />نسخ المشهد</button>
        <button type="button" disabled={disabled} className="admin-btn-ghost min-h-11" onClick={() => setEditing(!editing)}>
          {editing ? <Check className="h-4 w-4" /> : <Pencil className="h-4 w-4" />}{editing ? 'إنهاء التعديل' : 'تعديل الكادرات'}
        </button>
      </div>
    </header>
    <ol className="divide-y divide-[var(--admin-border)]">
      {scene.shots.map((shot, index) => <li key={`${selected}-${index}`} className="grid gap-3 py-6 sm:grid-cols-[5.5rem_minmax(0,1fr)] sm:gap-5">
        <span className="w-fit self-start rounded-lg bg-[var(--admin-card-soft)] px-3 py-2 text-sm font-black tabular-nums text-[var(--admin-primary)]" dir="ltr">
          {shotTime(shot.start)}–{shotTime(shot.end)}
        </span>
        <div className="min-w-0 space-y-3">
          {editing ? <fieldset disabled={disabled} className="space-y-3">
            <legend className="sr-only">الكادر {index + 1}</legend>
            {(['title', 'action', 'camera', 'dialogue', 'sound'] as const).map(field => <div key={field} className="text-sm font-bold text-[var(--admin-text)]">
              <label htmlFor={`mim-${selected}-${index}-${field}`}>
                {{ title: 'عنوان الكادر', action: 'الحركة', camera: 'الكاميرا', dialogue: 'الحوار', sound: 'الصوت' }[field]}
              </label>
              <textarea id={`mim-${selected}-${index}-${field}`} rows={field === 'action' || field === 'dialogue' ? 3 : 2} value={shot[field]} maxLength={field === 'title' ? 150 : field === 'action' ? 2000 : field === 'dialogue' ? 1500 : 1000}
                className={`${inputStyle} mt-1`} onChange={event => updateShot(index, field, event.target.value)} />
            </div>)}
          </fieldset> : <>
            <h4 className="text-lg font-extrabold text-[var(--admin-text)]">{shot.title}</h4>
            <p className="max-w-prose whitespace-pre-wrap break-words text-base leading-8 text-[var(--admin-text)]">{shot.action}</p>
            <p className="flex max-w-prose gap-2 text-sm leading-7 text-[var(--admin-muted)]"><Camera className="mt-1.5 h-4 w-4 shrink-0" aria-hidden="true" /><span>{shot.camera}</span></p>
            {shot.dialogue && <blockquote className="max-w-prose whitespace-pre-wrap break-words rounded-lg bg-[var(--admin-card-soft)] px-4 py-3 text-base font-bold leading-8 text-[var(--admin-text)]">{shot.dialogue}</blockquote>}
            {shot.sound && <p className="max-w-prose text-sm leading-7 text-[var(--admin-muted)]"><span className="font-bold">الصوت: </span>{shot.sound}</p>}
          </>}
        </div>
      </li>)}
    </ol>
    <details className="border-t border-[var(--admin-border)] pt-4">
      <summary className="min-h-11 cursor-pointer font-bold text-[var(--admin-text)]">برومبت المشهد الكامل مع آخر تعديلاتك</summary>
      <button type="button" className="admin-btn-ghost my-3 min-h-11" onClick={() => void copyStudioText(generationPrompt(document, selected))}><Copy className="h-4 w-4" />نسخ البرومبت</button>
      <pre dir="auto" className="max-h-96 overflow-y-auto whitespace-pre-wrap break-words rounded-lg bg-[var(--admin-card-soft)] p-4 font-sans text-sm leading-7 text-[var(--admin-text)]">{generationPrompt(document, selected)}</pre>
    </details>
  </article>;
}

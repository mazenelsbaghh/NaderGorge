'use client';

import Image from 'next/image';
import { useEffect, useState } from 'react';
import { Clapperboard, Copy, Download, Save } from 'lucide-react';
import toast from 'react-hot-toast';
import preparedEpisode from './prepared-episode.json';
import { characterReferences, lessonOpeningDirection, mcpBrief, preparedSourceMatches, type McpConnection, type MimDocument, type MimSnapshot, type MimSource } from './contract';
import { createEpisodeArchive } from './archive';
import { mimStudioService, studioError } from './service';
import { copyStudioText, MimScriptView } from './MimScriptView';
import { HiggsfieldConnection } from './HiggsfieldConnection';

const prepared = preparedEpisode as MimDocument;

export function LessonMimStudioTab({ lessonId }: { lessonId: string }) {
  const [snapshot, setSnapshot] = useState<MimSnapshot | null>(null);
  const [document, setDocument] = useState<MimDocument | null>(null);
  const [sources, setSources] = useState<MimSource[]>([]);
  const [sourceId, setSourceId] = useState('');
  const [connection, setConnection] = useState<McpConnection | null>(null);
  const [selected, setSelected] = useState(0);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);
  const source = sources.find(item => item.id === sourceId);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    void Promise.all([mimStudioService.read(lessonId, controller.signal), mimStudioService.sources(lessonId, controller.signal), mimStudioService.connection(controller.signal)])
      .then(([saved, options, linked]) => {
        if (controller.signal.aborted) return;
        setSnapshot(saved); setSources(options); setConnection(linked);
        const matching = options.find(item => preparedSourceMatches(prepared, item));
        setSourceId(saved?.sourceVideoId ?? matching?.id ?? '');
        setDocument(saved?.document ?? (matching ? structuredClone(prepared) : null));
        setDirty(!saved && Boolean(matching)); setSelected(0);
      }).catch(cause => { if (!controller.signal.aborted) setError(studioError(cause, 'تعذر تحميل استوديو الحصة.')); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [lessonId, retry]);

  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ''; };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  const save = async () => {
    if (!document || !source) return;
    setSaving(true); setError('');
    try {
      const saved = await mimStudioService.save(lessonId, snapshot?.version ?? null, source, document);
      setSnapshot(saved); setDocument(saved.document); setDirty(false); toast.success('اتحفظ الاسكربت في الحصة');
    } catch (cause) { setError(studioError(cause)); }
    finally { setSaving(false); }
  };

  const downloadPackage = async () => {
    if (!document || exporting) return;
    setExporting(true);
    try {
      const archive = await createEpisodeArchive(document);
      const url = URL.createObjectURL(new Blob([archive], { type: 'application/zip' }));
      const link = window.document.createElement('a');
      link.href = url; link.download = 'meem-papa-nader-video-package.zip'; link.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (cause) { toast.error(cause instanceof Error ? cause.message : 'تعذر تجهيز الحزمة. أعد المحاولة.'); }
    finally { setExporting(false); }
  };

  if (loading) return <div role="status" className="admin-panel p-6 text-[var(--admin-muted)]">جاري تحميل استوديو الحصة…</div>;
  if (!connection) return <div role="alert" className="admin-panel space-y-4 p-6"><p>{error}</p><button type="button" className="admin-btn-primary min-h-11" onClick={() => setRetry(value => value + 1)}>إعادة المحاولة</button></div>;

  return <section className="admin-panel p-4 sm:p-6" dir="rtl" aria-labelledby="mim-studio-heading">
    <header className="flex flex-wrap items-start justify-between gap-5 border-b border-[var(--admin-border)] pb-6">
      <div>
        <h2 id="mim-studio-heading" className="flex items-center gap-3 text-2xl font-black text-[var(--admin-text)]"><Clapperboard className="h-6 w-6 text-[var(--admin-primary)]" />استوديو ميم</h2>
        <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">قصة الحصة في ٤ مشاهد · ٣٠ ثانية لكل مشهد · ميم وبابا نادر</p>
        {source && <p className="mt-1 text-sm font-bold text-[var(--admin-text)]">مصدر الشرح: {source.title}</p>}
      </div>
      {document && <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-[var(--admin-muted)]" role="status">{dirty ? 'تعديلات لم تُحفظ' : 'محفوظ في الحصة'}</span>
        <button type="button" className="admin-btn-primary min-h-11 disabled:opacity-50" disabled={saving || !dirty || !source} onClick={() => void save()}><Save className="h-4 w-4" />{saving ? 'جاري الحفظ…' : 'حفظ الاسكربت'}</button>
      </div>}
    </header>
    {error && <p role="alert" className="mt-4 rounded-lg bg-red-50 p-4 text-sm leading-7 text-red-800">{error}</p>}
    {snapshot?.stale && <p role="alert" className="mt-4 rounded-lg bg-amber-50 p-4 text-sm leading-7 text-amber-900">شرح الفيديو اتغيّر بعد حفظ الاسكربت. راجع الفصول والحوار قبل إعادة الحفظ أو استخدامه للتوليد.</p>}
    <div className="grid gap-7 pt-6 xl:grid-cols-[16rem_minmax(0,1fr)]">
      <aside className="order-2 min-w-0 space-y-6 xl:order-1">
        {document && <nav aria-label="مشاهد الحلقة" className="hidden flex-col gap-2 xl:flex">
          {document.scenes.map((scene, index) => <button key={index} type="button" aria-current={selected === index ? 'step' : undefined}
            className={`min-h-14 rounded-lg px-3 py-3 text-start text-sm font-bold leading-6 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--admin-primary)] ${selected === index ? 'bg-[var(--admin-primary)] text-white' : 'bg-[var(--admin-card-soft)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}
            onClick={() => setSelected(index)}>{index + 1}. {scene.title}</button>)}
        </nav>}
        <section aria-labelledby="mim-references-heading">
          <h3 id="mim-references-heading" className="mb-3 font-black text-[var(--admin-text)]">شيتات الشخصيات</h3>
          <div className="grid grid-cols-2 gap-3 xl:grid-cols-1">
            {characterReferences.map(reference => <a key={reference.path} href={reference.path} target="_blank" rel="noopener noreferrer" className="min-w-0 rounded-lg focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)]">
              <Image src={reference.path} width={1536} height={1024} sizes="(min-width: 1280px) 256px, 40vw" alt={`شيت ${reference.name}: زوايا الجسم وتعبيرات الوجه`} className="aspect-[3/2] h-auto w-full rounded-lg object-contain bg-white" />
              <span className="mt-2 block text-sm font-bold text-[var(--admin-text)]">{reference.label}: {reference.name}</span>
              <span className="text-xs text-[var(--admin-primary)]">فتح الشيت بالحجم الكامل</span>
            </a>)}
          </div>
        </section>
        <HiggsfieldConnection connection={connection} onChanged={setConnection} unsaved={dirty} />
      </aside>
      <div className="order-1 min-w-0 xl:order-2">
        {document ? <>
          <nav aria-label="اختيار مشهد الحلقة" className="mb-5 grid grid-cols-2 gap-2 xl:hidden">
            {document.scenes.map((scene, index) => <button type="button" key={index} aria-current={selected === index ? 'step' : undefined}
              className={`min-h-12 rounded-lg px-3 py-2 text-start text-sm font-bold leading-6 ${selected === index ? 'bg-[var(--admin-primary)] text-white' : 'bg-[var(--admin-card-soft)] text-[var(--admin-text)]'}`}
              onClick={() => setSelected(index)}>{index + 1}. {scene.title}</button>)}
          </nav>
          <details className="mb-6 border-b border-[var(--admin-border)] pb-4">
            <summary className="min-h-11 cursor-pointer font-bold text-[var(--admin-text)]">القصة وثبات الشخصيات ومراجع الشرح</summary>
            <div className="max-w-prose space-y-3 text-sm leading-8 text-[var(--admin-muted)]"><p>{document.premise}</p><p>{document.style}</p><p>{document.continuity}</p>
              <p><strong className="text-[var(--admin-text)]">افتتاحية كل حصة: </strong>{lessonOpeningDirection}</p>
              <p>المصدر: ملخصات فصول الفيديو المحفوظة في الحصة، وليست مراجعة حرفية للتفريغ.</p>
              <ul className="list-inside list-disc">{source?.chapters.filter(chapter => document.scenes[selected].sourceChapterIds.includes(chapter.id)).map(chapter => <li key={chapter.id}>{chapter.title}</li>)}</ul>
            </div>
          </details>
          <MimScriptView key={selected} document={document} selected={selected} disabled={saving} onChange={next => { setDocument(next); setDirty(true); }} />
          <footer className="mt-7 flex flex-wrap gap-3 border-t border-[var(--admin-border)] pt-5">
            <button type="button" className="admin-btn-ghost min-h-11 disabled:opacity-50" disabled={exporting} onClick={() => void downloadPackage()}><Download className="h-4 w-4" />{exporting ? 'جاري تجهيز الشيتين والاسكربت…' : 'تنزيل الاسكربت والشيتين'}</button>
            <button type="button" className="admin-btn-ghost min-h-11" onClick={() => void copyStudioText(mcpBrief(document))}><Copy className="h-4 w-4" />نسخ طلب Higgsfield MCP</button>
            <p className="w-full text-sm leading-7 text-[var(--admin-muted)]">الحزمة فيها صور الشخصيتين الأصلية والاسكربت وبرومبت كل مشهد. نسخ الطلب ينسخ النص فقط؛ أرفق معه الشيتين بعد فك الحزمة.</p>
          </footer>
        </> : <div className="max-w-xl py-8">
          <h3 className="text-xl font-black text-[var(--admin-text)]">الحصة دي لسه مالهاش اسكربت محفوظ</h3>
          <p className="mt-3 text-base leading-8 text-[var(--admin-muted)]">المشاهد الأربعة اللي جهّزناها تخص «التحولات الكبرى في مصر خلال العصر الوسيط». هتظهر تلقائيًا في الحصة اللي فيها نفس فصول الشرح.</p>
          <p className="mt-3 text-sm leading-7 text-[var(--admin-muted)]">مراجع ميم وبابا نادر متاحة هنا، وربط Higgsfield خاص بحساب الإدارة.</p>
        </div>}
      </div>
    </div>
  </section>;
}

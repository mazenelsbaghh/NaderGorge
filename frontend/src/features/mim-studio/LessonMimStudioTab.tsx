'use client';

import Image from 'next/image';
import { useEffect, useState } from 'react';
import { Clapperboard, Copy, Download, Save, Sparkles, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import preparedEpisode from './prepared-episode.json';
import { characterReferences, lessonOpeningDirection, maximumSceneCount, targetSceneCount, mcpBrief, preparedSourceMatches, type McpConnection, type MimDocument, type MimSnapshot, type MimSource, type MimVideoModel } from './contract';
import { createEpisodeArchive } from './archive';
import { mimStudioService, studioError } from './service';
import { copyStudioText, MimScriptView } from './MimScriptView';
import { MimSceneVideoPanel } from './MimSceneVideoPanel';
import { HiggsfieldConnection } from './HiggsfieldConnection';
import { MimEpisodeVideoPanel } from './MimEpisodeVideoPanel';

const prepared = preparedEpisode as MimDocument;

export function LessonMimStudioTab({ lessonId }: { lessonId: string }) {
  const [snapshot, setSnapshot] = useState<MimSnapshot | null>(null);
  const [document, setDocument] = useState<MimDocument | null>(null);
  const [sources, setSources] = useState<MimSource[]>([]);
  const [sourceId, setSourceId] = useState('');
  const [sourceMode, setSourceMode] = useState<'video' | 'text'>('video');
  const [models, setModels] = useState<MimVideoModel[]>([]);
  const [model, setModel] = useState('wan3_0_prime');
  const [sourceText, setSourceText] = useState('');
  const [sceneCount, setSceneCount] = useState(4);
  const [episodeContext, setEpisodeContext] = useState('');
  const [generating, setGenerating] = useState(false);
  const [connection, setConnection] = useState<McpConnection | null>(null);
  const [selected, setSelected] = useState(0);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);
  const source = sources.find(item => item.id === sourceId);
  const target = document?.scenes.length ? targetSceneCount(document) : sceneCount;

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    void Promise.all([mimStudioService.read(lessonId, controller.signal), mimStudioService.sources(lessonId, controller.signal), mimStudioService.connection(controller.signal), mimStudioService.models(controller.signal)])
      .then(([saved, options, linked, videoModels]) => {
        if (controller.signal.aborted) return;
        setSnapshot(saved); setSources(options); setConnection(linked); setModels(videoModels);
        const matching = options.find(item => preparedSourceMatches(prepared, item));
        setSourceId(saved ? saved.sourceVideoId ?? '' : matching?.id ?? '');
        setSourceMode(saved && !saved.sourceVideoId ? 'text' : 'video');
        setSourceText(saved?.document.sourceText ?? '');
        setDocument(saved?.document ?? (matching ? structuredClone(prepared) : null));
        setSceneCount(saved ? targetSceneCount(saved.document) : 4);
        setEpisodeContext(saved?.document.episodeContext ?? '');
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
    if (!document || !document.scenes.length) return;
    setSaving(true); setError('');
    try {
      const saved = await mimStudioService.save(lessonId, snapshot?.version ?? null, source, document);
      setSnapshot(saved); setDocument(saved.document); setDirty(false); toast.success('اتحفظ الاسكربت في الحصة');
    } catch (cause) { setError(studioError(cause)); }
    finally { setSaving(false); }
  };

  const refresh = async () => {
    try {
      const saved = await mimStudioService.read(lessonId);
      if (saved) { setSnapshot(saved); setDocument(saved.document); setDirty(false); setSelected(Math.max(0, saved.document.scenes.length - 1)); }
    } catch (cause) { setError(studioError(cause)); }
  };
  const generateNext = async () => {
    if (generating || dirty) return;
    setGenerating(true); setError('');
    try {
      const saved = await mimStudioService.generateNext(lessonId, snapshot?.version ?? null, source,
        source ? null : sourceText, { expectedSceneCount: document?.scenes.length ?? 0, targetSceneCount: target,
          episodeContext: document?.scenes.length ? document.episodeContext ?? null : episodeContext.trim() || null });
      setSnapshot(saved); setDocument(saved.document); setDirty(false); setSelected(saved.document.scenes.length - 1);
      toast.success('المشهد جاهز ومحفوظ. راجعه قبل كتابة اللي بعده.');
    } catch (cause) { await refresh(); setError(studioError(cause, 'تعذر كتابة المشهد. حدّث الحالة قبل إعادة المحاولة.')); }
    finally { setGenerating(false); }
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
        <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">حلقة من {target} مشاهد · ٣٠ ثانية لكل مشهد · المدة النهائية {target * 30} ثانية</p>
        {source && <p className="mt-1 text-sm font-bold text-[var(--admin-text)]">مصدر الشرح: {source.title}</p>}
      </div>
      {document && <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-[var(--admin-muted)]" role="status">{dirty ? 'تعديلات لم تُحفظ' : 'محفوظ في الحصة'}</span>
        <button type="button" className="admin-btn-primary min-h-11 disabled:opacity-50" disabled={saving || generating || snapshot?.generating || !dirty || !document.scenes.length} onClick={() => void save()}><Save className="h-4 w-4" />{saving ? 'جاري الحفظ…' : 'حفظ الاسكربت'}</button>
      </div>}
    </header>
    {error && <p role="alert" className="mt-4 rounded-lg bg-red-50 p-4 text-sm leading-7 text-red-800">{error}</p>}
    {snapshot?.stale && <p role="alert" className="mt-4 rounded-lg bg-amber-50 p-4 text-sm leading-7 text-amber-900">شرح الفيديو اتغيّر بعد حفظ الاسكربت. راجع الفصول والحوار قبل إعادة الحفظ أو استخدامه للتوليد.</p>}
    <div className="space-y-2 border-b border-[var(--admin-border)] py-5">
      <label htmlFor="mim-video-model" className="block text-sm font-bold text-[var(--admin-text)]">موديل توليد الفيديو</label>
      <select id="mim-video-model" value={model} onChange={event => setModel(event.target.value)} className="admin-input min-h-11 w-full max-w-xl">
        {models.map(option => <option key={option.id} value={option.id}>{option.name}</option>)}
      </select>
      <p className="text-sm leading-7 text-[var(--admin-muted)]">جيمناي يكتب الاسكربت من ملخصات الفيديو اللي تختاره. الموديل هنا يحوّل المشهد لفيديو بالصوت ومراجع ميم ونادر؛ التكلفة بتظهر قبل التوليد.</p>
    </div>
    <section aria-label="كتابة المشاهد" className="space-y-4 border-b border-[var(--admin-border)] py-6">
      <div className="max-w-3xl space-y-3">
        {!snapshot && !!document?.scenes.length && <button type="button" className="admin-btn-ghost min-h-11" disabled={saving || generating}
          onClick={() => { setDocument(null); setDirty(false); setSelected(0); }}>اختيار موقف وعدد مشاهد لحلقة جديدة</button>}
        <label htmlFor="mim-scene-count" className="block text-sm font-bold text-[var(--admin-text)]">عدد مشاهد الحلقة</label>
        <select id="mim-scene-count" value={target} disabled={!!document?.scenes.length || generating || snapshot?.generating}
          onChange={event => setSceneCount(Number(event.target.value))} className="admin-input min-h-11 w-full max-w-xs">
          {Array.from({ length: maximumSceneCount }, (_, index) => index + 1).map(count => <option key={count} value={count}>{count} مشاهد — {count * 30} ثانية</option>)}
        </select>
        <label htmlFor="mim-episode-context" className="block text-sm font-bold text-[var(--admin-text)]">موقف الحلقة <span className="font-normal">(اختياري)</span></label>
        <textarea id="mim-episode-context" rows={3} maxLength={2000} value={document?.scenes.length ? document.episodeContext ?? '' : episodeContext}
          disabled={!!document?.scenes.length || generating || snapshot?.generating} onChange={event => setEpisodeContext(event.target.value)}
          className="admin-input w-full leading-7" placeholder="مثال: نادر وميم بيطبخوا، وميم لخبط المقادير. أو سيبه فاضي ليختار جيمناي موقف يناسب محتوى الحصة." />
        <p className="text-sm leading-7 text-[var(--admin-muted)]">جيمناي يوزّع محتوى الحصة على العدد ده داخل قصة واحدة، وكل مشهد يكمل من نهاية السابق. العدد والموقف بيتثبتوا بعد أول مشهد. الفيديو من غير أي كتابة ظاهرة.</p>
      </div>
      {!document?.scenes.length && <>
        <h3 className="text-lg font-black text-[var(--admin-text)]">ابدأ بمصدر شرح الحصة</h3>
        <p className="max-w-prose text-sm leading-7 text-[var(--admin-muted)]">اختار فيديو الشرح من الحصة. جيمناي هيحلل ملخصات فصوله ويكتب مشهد واحد مدته ٣٠ ثانية، وبعد مراجعته تقدر تبدأ التالي.</p>
        <fieldset className="flex flex-wrap gap-5 text-sm text-[var(--admin-text)]" disabled={generating || snapshot?.generating}>
          <legend className="mb-3 font-bold">مصدر الشرح</legend>
          <label className="flex min-h-11 items-center gap-2"><input type="radio" name="mim-source-mode" checked={sourceMode === 'video'} onChange={() => setSourceMode('video')} />اختيار فيديو من الحصة</label>
          <label className="flex min-h-11 items-center gap-2"><input type="radio" name="mim-source-mode" checked={sourceMode === 'text'} onChange={() => { setSourceMode('text'); setSourceId(''); }} />إدخال نص يدويًا</label>
        </fieldset>
        {sourceMode === 'video' && <div className="max-w-3xl space-y-3">
          <label htmlFor="mim-source" className="block text-sm font-bold text-[var(--admin-text)]">فيديو الشرح</label>
          <select id="mim-source" disabled={generating || snapshot?.generating || !sources.length} value={sourceId} onChange={event => setSourceId(event.target.value)} className="admin-input min-h-11 w-full">
            <option value="">{sources.length ? 'اختار الفيديو اللي عايز تكتب منه المشاهد' : 'الحصة دي مفيهاش فيديوهات مفعّلة'}</option>
            {sources.map(item => <option key={item.id} value={item.id} disabled={!item.chapters.some(chapter => chapter.summary?.trim())}>{item.title}{!item.chapters.some(chapter => chapter.summary?.trim()) ? ' — يحتاج تحليل AI' : ''}</option>)}
          </select>
          {!sources.length && <p className="text-sm leading-7 text-[var(--admin-muted)]">أضف فيديو للحصة من تبويب الفيديوهات، وبعد تحليل AI ارجع هنا لاختياره.</p>}
          {sources.length > 0 && !sources.some(item => item.chapters.some(chapter => chapter.summary?.trim())) && <p className="text-sm leading-7 text-[var(--admin-muted)]">الفيديوهات لسه ملهاش ملخصات. افتح تبويب «تحليل AI» وحلّل فيديو الشرح أولًا، وبعدها حدّث القائمة.</p>}
          <button type="button" className="admin-btn-ghost min-h-11" disabled={generating || snapshot?.generating} onClick={() => setRetry(value => value + 1)}><RefreshCw className="h-4 w-4" />تحديث قائمة الفيديوهات</button>
          {source && <details className="text-sm leading-7 text-[var(--admin-muted)]"><summary className="min-h-11 cursor-pointer font-bold text-[var(--admin-text)]">عرض الملخصات اللي جيمناي هيستخدمها ({source.chapters.length})</summary>
            {source.chapters.map(chapter => <div key={chapter.id} className="my-3"><h4 className="font-bold">{chapter.title}</h4><p className="whitespace-pre-wrap">{chapter.summary}</p></div>)}
          </details>}
        </div>}
        {sourceMode === 'text' && <div className="max-w-3xl space-y-2">
          <label htmlFor="mim-source-text" className="block text-sm font-bold text-[var(--admin-text)]">نص شرح الحصة</label>
          <textarea id="mim-source-text" value={sourceText} disabled={generating || snapshot?.generating} onChange={event => setSourceText(event.target.value)} rows={7} maxLength={24000}
            className="admin-input w-full leading-8" placeholder="الصق شرح الحصة أو ملخصها التفصيلي هنا…" />
          <p className="text-sm text-[var(--admin-muted)]">من ١٠٠ إلى ٢٤ ألف حرف. بنستخدم النص في كتابة المشاهد، عنوان الحصة وحده مش كفاية.</p>
        </div>}
      </>}
      <div className="flex flex-wrap items-center gap-3">
        {(document?.scenes.length ?? 0) < target && <button type="button" onClick={() => void generateNext()}
          disabled={generating || saving || dirty || snapshot?.generating || snapshot?.stale || (sourceMode === 'video' ? !source || !source.chapters.some(chapter => chapter.summary?.trim()) : sourceText.trim().length < 100)}
          className="admin-btn-primary min-h-11 disabled:opacity-50"><Sparkles className="h-4 w-4" />
          {generating || snapshot?.generating ? 'جاري كتابة المشهد…' : document?.scenes.length ? `كتابة المشهد التالي (${document.scenes.length + 1} من ${target})` : 'كتابة المشهد الأول'}
        </button>}
        {snapshot && !dirty && <button type="button" onClick={() => void refresh()} disabled={generating} className="admin-btn-ghost min-h-11"><RefreshCw className="h-4 w-4" />تحديث الحالة</button>}
        <p role="status" className="text-sm leading-7 text-[var(--admin-muted)]">{dirty ? 'احفظ تعديلاتك قبل كتابة المشهد التالي.' : document?.scenes.length ? `${document.scenes.length} من ${target} مشاهد محفوظة. راجع الحوار والحركة قبل المتابعة.` : 'كل ضغطة تكتب مشهد واحد فقط.'}</p>
      </div>
    </section>
    <div className="grid gap-7 pt-6 xl:grid-cols-[16rem_minmax(0,1fr)]">
      <aside className="order-2 min-w-0 space-y-6 xl:order-1">
        {document && document.scenes.length > 0 && <nav aria-label="مشاهد الحلقة" className="hidden flex-col gap-2 xl:flex">
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
        {document && document.scenes.length > 0 ? <>
          <nav aria-label="اختيار مشهد الحلقة" className="mb-5 grid grid-cols-2 gap-2 xl:hidden">
            {document.scenes.map((scene, index) => <button type="button" key={index} aria-current={selected === index ? 'step' : undefined}
              className={`min-h-12 rounded-lg px-3 py-2 text-start text-sm font-bold leading-6 ${selected === index ? 'bg-[var(--admin-primary)] text-white' : 'bg-[var(--admin-card-soft)] text-[var(--admin-text)]'}`}
              onClick={() => setSelected(index)}>{index + 1}. {scene.title}</button>)}
          </nav>
          <details className="mb-6 border-b border-[var(--admin-border)] pb-4">
            <summary className="min-h-11 cursor-pointer font-bold text-[var(--admin-text)]">القصة وثبات الشخصيات ومراجع الشرح</summary>
            <div className="max-w-prose space-y-3 text-sm leading-8 text-[var(--admin-muted)]"><p>{document.premise}</p><p>{document.style}</p><p>{document.continuity}</p>
              <p><strong className="text-[var(--admin-text)]">افتتاحية كل حصة: </strong>{lessonOpeningDirection}</p>
              <p>{document.sourceText ? 'المصدر: نص الشرح اللي أضفته للحصة.' : 'المصدر: ملخصات فصول الفيديو المحفوظة في الحصة، وليست مراجعة حرفية للتفريغ.'}</p>
              <ul className="list-inside list-disc">{source?.chapters.filter(chapter => document.scenes[selected].sourceChapterIds.includes(chapter.id)).map(chapter => <li key={chapter.id}>{chapter.title}</li>)}</ul>
            </div>
          </details>
          <MimScriptView key={selected} document={document} selected={selected} disabled={saving || generating || snapshot?.generating} onChange={next => { setDocument(next); setDirty(true); }} />
          <MimSceneVideoPanel key={`video-${selected}`} lessonId={lessonId} scene={selected} scriptVersion={snapshot?.version ?? null}
            model={model} connected={connection.connected} disabled={dirty || saving || generating || !!snapshot?.generating || !!snapshot?.stale} />
          <MimEpisodeVideoPanel lessonId={lessonId} scriptVersion={snapshot?.version ?? null} sceneCount={target}
            complete={document.scenes.length === target} disabled={dirty || saving || generating || !!snapshot?.generating || !!snapshot?.stale} />
          <footer className="mt-7 flex flex-wrap gap-3 border-t border-[var(--admin-border)] pt-5">
            <button type="button" className="admin-btn-ghost min-h-11 disabled:opacity-50" disabled={exporting} onClick={() => void downloadPackage()}><Download className="h-4 w-4" />{exporting ? 'جاري تجهيز الشيتين والاسكربت…' : 'تنزيل الاسكربت والشيتين'}</button>
            <button type="button" className="admin-btn-ghost min-h-11" onClick={() => void copyStudioText(mcpBrief(document))}><Copy className="h-4 w-4" />نسخ طلب Higgsfield MCP</button>
            <p className="w-full text-sm leading-7 text-[var(--admin-muted)]">الحزمة فيها صور الشخصيتين الأصلية والاسكربت وبرومبت كل مشهد. نسخ الطلب ينسخ النص فقط؛ أرفق معه الشيتين بعد فك الحزمة.</p>
          </footer>
        </> : <div className="max-w-xl py-8">
          <h3 className="text-xl font-black text-[var(--admin-text)]">المشهد الأول هيظهر هنا</h3>
          <p className="mt-3 text-base leading-8 text-[var(--admin-muted)]">بعد إضافة مصدر الشرح فوق، اضغط «كتابة المشهد الأول». هتلاقي كل كادر بتوقيته وحركته وحواره وزاوية الكاميرا.</p>
          <p className="mt-3 text-sm leading-7 text-[var(--admin-muted)]">مراجع ميم وبابا نادر متاحة هنا، وربط Higgsfield خاص بحساب الإدارة.</p>
        </div>}
      </div>
    </div>
  </section>;
}

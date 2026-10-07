import { characterReferences, episodeScript, generationPrompt, mcpBrief, type MimDocument } from './contract.ts';

export async function createEpisodeArchive(document: MimDocument): Promise<Uint8Array<ArrayBuffer>> {
  const [{ default: JSZip }, images] = await Promise.all([
    import('jszip'),
    Promise.all(characterReferences.map(async reference => {
      const response = await fetch(reference.path, { signal: AbortSignal.timeout(30000) });
      if (!response.ok) throw new Error(`تعذر تحميل شيت ${reference.name}. أعد المحاولة.`);
      const bytes = new Uint8Array(await response.arrayBuffer());
      const pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
      if (!pngSignature.every((byte, index) => bytes[index] === byte)) {
        throw new Error(`ملف شيت ${reference.name} غير صالح. لم يتم تنزيل حزمة ناقصة.`);
      }
      return { reference, bytes };
    })),
  ]);
  const zip = new JSZip();
  for (const { reference, bytes } of images) zip.file(reference.fileName, bytes);
  zip.file('episode-script.txt', '\uFEFF' + episodeScript(document));
  zip.file('higgsfield-mcp-request.txt', '\uFEFF' + mcpBrief(document));
  document.scenes.forEach((_, index) => zip.file(`scene-${index + 1}-prompt.txt`, '\uFEFF' + generationPrompt(document, index)));
  zip.file('read-me.txt', '\uFEFF' + [
    'حزمة ميم وبابا نادر',
    'تحتوي الحزمة على الاسكربت الحالي، برومبت لكل مشهد، وملفي الصور الأصليين دون تعديل.',
    'فك ضغط الحزمة، وأرفق شيت ميم أولًا ثم شيت بابا نادر مع طلب Higgsfield MCP.',
    'تعليمات الإرفاق موجودة في كل برومبت؛ الحزمة نفسها لا تبدأ التوليد ولا ترسل الملفات تلقائيًا.',
  ].join('\n'));
  return new Uint8Array(await zip.generateAsync({ type: 'uint8array' }));
}

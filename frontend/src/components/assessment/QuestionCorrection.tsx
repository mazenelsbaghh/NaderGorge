import { resolveMediaUrl } from '@/utils/resolve-media-url';

export function QuestionCorrection({
  writtenCorrection,
  audioUrl,
}: {
  writtenCorrection?: string | null;
  audioUrl?: string | null;
}) {
  if (!writtenCorrection?.trim() && !audioUrl?.trim()) {
    return null;
  }

  return (
    <div className="mt-3 border-t border-border/30 pt-3">
      <p className="text-xs font-black uppercase tracking-widest text-muted-foreground">
        تصحيح السؤال
      </p>
      {writtenCorrection?.trim() && (
        <p className="mt-1.5 whitespace-pre-wrap text-sm font-bold leading-6 text-foreground">
          {writtenCorrection}
        </p>
      )}
      {audioUrl?.trim() && (
        <div className="mt-3">
          <p className="mb-2 text-xs font-black text-muted-foreground">التصحيح الصوتي</p>
          <audio controls className="h-9 w-full" preload="none">
            <source src={resolveMediaUrl(audioUrl)} />
          </audio>
        </div>
      )}
    </div>
  );
}

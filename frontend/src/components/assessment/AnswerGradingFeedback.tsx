export function AnswerGradingFeedback({ feedback }: { feedback?: string | null }) {
  if (!feedback) return null;
  return (
    <div className="mt-3 rounded-xl border border-border/40 bg-background/60 p-4">
      <p className="text-xs font-black text-muted-foreground">تعليل التصحيح</p>
      <p className="mt-1.5 whitespace-pre-wrap text-sm leading-7 text-foreground" dir="auto">{feedback}</p>
    </div>
  );
}

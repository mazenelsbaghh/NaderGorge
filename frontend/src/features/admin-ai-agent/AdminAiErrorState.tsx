export function AdminAiErrorState({
  message,
  onRetry,
}: {
  message: string;
  onRetry: () => void;
}) {
  return (
    <div role="alert" className="m-auto max-w-md p-6 text-center">
      <h2 className="font-black">تعذر تحميل وكيل الإدارة</h2>
      <p className="mt-2 text-sm text-[var(--admin-muted)]">{message}</p>
      <button
        onClick={onRetry}
        className="mt-4 min-h-11 rounded-xl bg-[var(--admin-primary)] px-5 font-black text-[var(--admin-primary-contrast)]"
      >
        إعادة المحاولة
      </button>
    </div>
  );
}

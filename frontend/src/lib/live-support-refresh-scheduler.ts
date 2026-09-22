// A burst of receipts must not cancel the snapshot that is already loading.
export function createLiveSupportRefreshScheduler(refresh: () => Promise<void>, onError: () => void) {
  let timer: ReturnType<typeof setTimeout> | undefined;
  let running = false;
  let pending = false;
  let disposed = false;

  const request = () => {
    if (disposed) return;
    pending = true;
    if (timer || running) return;
    timer = setTimeout(() => {
      timer = undefined;
      pending = false;
      running = true;
      void refresh().catch(onError).finally(() => {
        running = false;
        if (pending) request();
      });
    }, 150);
  };

  return {
    request,
    dispose: () => {
      disposed = true;
      pending = false;
      if (timer) clearTimeout(timer);
    },
  };
}

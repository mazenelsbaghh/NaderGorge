// One budget for exam and homework requests in each worker process.
export const WRITTEN_GRADING_CONCURRENCY = 6;

let activeGradingRequests = 0;
const waitingForSlot: Array<() => void> = [];

export async function withWrittenGradingSlot<T>(grade: () => Promise<T>): Promise<T> {
  if (activeGradingRequests >= WRITTEN_GRADING_CONCURRENCY) {
    await new Promise<void>(resolve => waitingForSlot.push(resolve));
  } else {
    activeGradingRequests++;
  }
  try {
    return await grade();
  } finally {
    const next = waitingForSlot.shift();
    if (next) next();
    else activeGradingRequests--;
  }
}

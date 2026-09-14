export type MutationLock = { current: boolean };

export function acquireMutationLock(lock: MutationLock): boolean {
  if (lock.current) return false;
  lock.current = true;
  return true;
}

export function releaseMutationLock(lock: MutationLock): void {
  lock.current = false;
}

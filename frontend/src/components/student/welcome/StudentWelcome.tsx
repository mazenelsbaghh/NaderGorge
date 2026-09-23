'use client';

import { useEffect, useRef } from 'react';
import { useAuthStore } from '@/stores/auth-store';
import { studentWelcomeService, type WelcomeClaim } from '@/services/student-welcome-service';
import styles from './StudentWelcome.module.css';

type Pose = 'bottom' | 'right' | 'left';
const poses: Pose[] = ['bottom', 'right', 'left'];
const cairoDay = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Cairo' }).format(new Date());

export function StudentWelcome({ onSettled }: { onSettled: () => void }) {
  const user = useAuthStore(state => state.user);
  const userId = user?.id;
  const isStudent = user?.roles.includes('Student');
  const dialogRef = useRef<HTMLDialogElement>(null);
  const frameRef = useRef<HTMLIFrameElement>(null);

  useEffect(() => {
    if (!userId || !isStudent) { onSettled(); return; }
    const dialog = dialogRef.current!;
    const frame = frameRef.current!;
    let disposed = false;
    let requesting = false;
    let closing = false;
    let claim: WelcomeClaim | null = null;
    let checkedDay: string | null = null;
    let previousPose: Pose | null = null;
    let expiryTimer: ReturnType<typeof setTimeout> | undefined;
    const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)');
    const release = (token: string) => studentWelcomeService.release(token).catch(() => undefined);

    async function finish(completed: boolean) {
      if (!claim || closing) return;
      closing = true;
      const receipt = claim;
      clearTimeout(expiryTimer);
      frame.contentWindow?.postMessage({ type: 'mim-stop' }, location.origin);
      dialog.dataset.closing = 'true';
      // Closing never depends on the network; a failed receipt remains eligible on a later visit.
      const save = async () => {
        if (!completed) { await release(receipt.token); return; }
        for (let attempt = 0; attempt < 3; attempt++) {
          try { if (await studentWelcomeService.complete(receipt.token)) return; }
          catch { /* Retry the same receipt; completion is idempotent. */ }
          await new Promise(resolve => setTimeout(resolve, 500 * (attempt + 1)));
        }
        await release(receipt.token);
      };
      void save();
      await new Promise(resolve => setTimeout(resolve, reduceMotion.matches ? 0 : 480));
      if (disposed) return;
      dialog.close();
      frame.removeAttribute('src');
      claim = null;
      closing = false;
      delete dialog.dataset.closing;
      onSettled();
    }

    async function check() {
      if (disposed || requesting || claim || document.hidden || checkedDay === cairoDay()) return;
      // Respect existing contract, security and other student dialogs before greeting.
      if (document.querySelector('[aria-modal="true"], dialog[open], #ins-modal-title')) return;
      requesting = true;
      try {
        const result = await studentWelcomeService.claim();
        if (disposed) { if (result) await release(result.token); return; }
        if (!result) { checkedDay = cairoDay(); onSettled(); return; }
        if (document.hidden || document.querySelector('[aria-modal="true"], dialog[open], #ins-modal-title')) {
          await release(result.token);
          return;
        }
        claim = result;
        checkedDay = cairoDay();
        const options = poses.filter(pose => pose !== previousPose);
        const pose = options[Math.floor(Math.random() * options.length)];
        previousPose = pose;
        dialog.dataset.pose = pose;
        frame.src = `/mim-welcome/embedded.html?welcome=${result.kind}&pose=${pose}`;
        dialog.showModal();
        // A stalled download/autoplay prompt cannot hold the UI or a database lease forever.
        expiryTimer = setTimeout(() => void finish(false), 120_000);
      } catch {
        // Welcome is optional: failures must not block access to study material.
        onSettled();
      } finally { requesting = false; }
    }
    function message(event: MessageEvent) {
      if (event.origin !== location.origin || event.source !== frame.contentWindow) return;
      if (event.data?.type === 'mim-complete') void finish(true);
      if (event.data?.type === 'mim-dismiss') void finish(false);
    }
    function cancel(event: Event) { event.preventDefault(); void finish(false); }
    function visibility() {
      if (document.hidden && claim) void finish(false);
      else void check();
    }
    const startup = setTimeout(() => void check(), 500);
    const poll = setInterval(() => { if (checkedDay === null) void check(); }, 60_000);
    window.addEventListener('message', message);
    window.addEventListener('focus', check);
    document.addEventListener('visibilitychange', visibility);
    dialog.addEventListener('cancel', cancel);
    return () => {
      disposed = true;
      clearTimeout(startup);
      clearTimeout(expiryTimer);
      clearInterval(poll);
      window.removeEventListener('message', message);
      window.removeEventListener('focus', check);
      document.removeEventListener('visibilitychange', visibility);
      dialog.removeEventListener('cancel', cancel);
      frame.contentWindow?.postMessage({ type: 'mim-stop' }, location.origin);
      if (claim && !closing) void release(claim.token);
      dialog.close();
      frame.removeAttribute('src');
    };
  }, [userId, isStudent, onSettled]);

  return <dialog ref={dialogRef} className={styles.dialog} aria-label="ترحيب ميم في مسار">
    <iframe ref={frameRef} title="ميم يرحّب بيك" allow="autoplay" className={styles.frame} />
  </dialog>;
}

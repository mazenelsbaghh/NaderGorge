'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Gamepad2, Maximize2, Minimize2, X } from 'lucide-react';

import {
  mimGameProgressKeyForContent,
  type MimGameContent,
} from '@/lib/mim-game-contract';

const FRAME_PATH = '/mim-game/index.html';

type FrameEvent = {
  source: 'massar-mim-game';
  type: 'ready' | 'close' | 'error';
  message?: string;
};

export function LessonMimGameFrame({
  content,
  progressKey,
  mode,
  triggerLabel,
}: {
  content: MimGameContent;
  progressKey: string;
  mode: 'student' | 'preview';
  triggerLabel?: string;
}) {
  const [open, setOpen] = useState(false);
  const [frameReady, setFrameReady] = useState(false);
  const [frameError, setFrameError] = useState('');
  const [fullscreen, setFullscreen] = useState(false);
  const [fullscreenNotice, setFullscreenNotice] = useState('');
  const [scopedProgressKey, setScopedProgressKey] = useState('');
  const dialogRef = useRef<HTMLDivElement>(null);
  const iframeRef = useRef<HTMLIFrameElement>(null);

  const unlockOrientation = useCallback(() => {
    const orientation = screen.orientation as
      | (ScreenOrientation & {
          unlock?: () => void;
        })
      | undefined;
    orientation?.unlock?.();
  }, []);

  const lockLandscape = useCallback(async () => {
    const orientation = screen.orientation as
      | (ScreenOrientation & {
          lock?: (value: 'landscape') => Promise<void>;
        })
      | undefined;
    try {
      await orientation?.lock?.('landscape');
    } catch {
      // Fullscreen still works when a browser requires manual device rotation.
    }
  }, []);

  const close = useCallback(() => {
    iframeRef.current?.contentWindow?.postMessage(
      { source: 'massar-platform', type: 'dispose' },
      window.location.origin
    );
    if (document.fullscreenElement === dialogRef.current) {
      void document.exitFullscreen().catch(() => undefined);
    }
    unlockOrientation();
    setOpen(false);
    setFrameReady(false);
    setFrameError('');
    setFullscreen(false);
    setFullscreenNotice('');
    setScopedProgressKey('');
  }, [unlockOrientation]);

  const toggleFullscreen = useCallback(async () => {
    try {
      setFullscreenNotice('');
      if (document.fullscreenElement === dialogRef.current) {
        await document.exitFullscreen();
        unlockOrientation();
        return;
      }

      if (!dialogRef.current?.requestFullscreen) {
        setFullscreenNotice(
          'المتصفح لا يدعم ملء الشاشة. لف الهاتف بالعرض واستمر من هذه الشاشة.'
        );
        return;
      }
      await dialogRef.current.requestFullscreen();
      await lockLandscape();
    } catch {
      setFullscreen(false);
      setFullscreenNotice(
        'تعذر تشغيل ملء الشاشة تلقائيًا. لف الهاتف بالعرض واستمر من هذه الشاشة.'
      );
    }
  }, [lockLandscape, unlockOrientation]);

  const openGame = useCallback(async () => {
    try {
      setScopedProgressKey(
        await mimGameProgressKeyForContent(progressKey, content)
      );
      setOpen(true);
    } catch {
      setFrameError('تعذر تجهيز حفظ التقدم على هذا الجهاز.');
      setOpen(true);
    }
  }, [content, progressKey]);

  useEffect(() => {
    if (!open) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const onMessage = (event: MessageEvent<FrameEvent>) => {
      if (
        event.origin !== window.location.origin ||
        event.source !== iframeRef.current?.contentWindow
      )
        return;
      if (!event.data || event.data.source !== 'massar-mim-game') return;
      if (event.data.type === 'ready') {
        setFrameReady(true);
        iframeRef.current?.contentWindow?.postMessage(
          {
            source: 'massar-platform',
            type: 'bootstrap',
            payload: { content, progressKey: scopedProgressKey, mode },
          },
          window.location.origin
        );
      } else if (event.data.type === 'close') {
        close();
      } else if (event.data.type === 'error') {
        setFrameError(
          event.data.message || 'تعذر تشغيل اللعبة على هذا الجهاز.'
        );
      }
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close();
    };
    window.addEventListener('message', onMessage);
    window.addEventListener('keydown', onKeyDown);
    const onFullscreenChange = () => {
      const active = document.fullscreenElement === dialogRef.current;
      setFullscreen(active);
      if (!active) unlockOrientation();
    };
    document.addEventListener('fullscreenchange', onFullscreenChange);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener('message', onMessage);
      window.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('fullscreenchange', onFullscreenChange);
    };
  }, [close, content, mode, open, scopedProgressKey, unlockOrientation]);

  return (
    <>
      <button
        type="button"
        onClick={() => void openGame()}
        className="admin-btn-primary min-h-12 px-6"
      >
        <Gamepad2 className="h-5 w-5" aria-hidden="true" />
        {triggerLabel ??
          (mode === 'preview' ? 'معاينة المسودة' : 'ابدأ لعبة الحصة')}
      </button>
      {open && (
        <div
          ref={dialogRef}
          className="fixed inset-0 z-[70] flex flex-col bg-[#07162d]"
          role="dialog"
          aria-modal="true"
          aria-label={mode === 'preview' ? 'معاينة لعبة الحصة' : 'لعبة الحصة'}
        >
          <div
            className={
              fullscreen
                ? 'hidden'
                : 'flex min-h-14 items-center justify-between gap-3 border-b border-white/15 bg-[#0A1D3D] px-3 text-white sm:px-5'
            }
          >
            <div className="min-w-0">
              <p className="truncate text-sm font-black">{content.title}</p>
              <p className="text-xs text-white/75">
                {mode === 'preview'
                  ? 'معاينة إدارية، غير منشورة للطلاب'
                  : 'مراجعة تدريبية، لا تؤثر على الدرجات'}
              </p>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <button
                type="button"
                onClick={() => void toggleFullscreen()}
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-white/10 px-3 text-sm font-bold text-white transition-colors hover:bg-white/20 focus-visible:ring-2 focus-visible:ring-[#49caca]"
                aria-label={fullscreen ? 'الخروج من ملء الشاشة' : 'ملء الشاشة بالعرض'}
                aria-pressed={fullscreen}
              >
                {fullscreen ? (
                  <Minimize2 className="h-5 w-5" aria-hidden="true" />
                ) : (
                  <Maximize2 className="h-5 w-5" aria-hidden="true" />
                )}
                <span className="hidden sm:inline">
                  {fullscreen ? 'تصغير' : 'ملء الشاشة'}
                </span>
              </button>
              <button
                type="button"
                onClick={close}
                className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-xl bg-white/10 text-white transition-colors hover:bg-white/20 focus-visible:ring-2 focus-visible:ring-[#49caca]"
                aria-label="إغلاق لعبة الحصة"
              >
                <X className="h-5 w-5" aria-hidden="true" />
              </button>
            </div>
          </div>
          <div className="relative min-h-0 flex-1">
            {fullscreenNotice && (
              <p
                role="status"
                className="absolute inset-x-3 top-3 z-20 rounded-xl bg-amber-100 px-4 py-3 text-center text-sm font-bold text-amber-950 shadow-lg"
              >
                {fullscreenNotice}
              </p>
            )}
            {!frameReady && !frameError && (
              <div className="absolute inset-0 z-10 grid place-items-center bg-[#07162d] text-sm font-bold text-white">
                جاري تجهيز عالم ميم…
              </div>
            )}
            {frameError ? (
              <div className="grid h-full place-items-center p-6 text-center text-white">
                <div>
                  <p className="text-lg font-black">تعذر تشغيل اللعبة</p>
                  <p className="mt-2 text-sm text-white/75">{frameError}</p>
                  <button
                    type="button"
                    onClick={close}
                    className="mt-5 min-h-11 rounded-xl bg-white px-5 font-bold text-[#0A1D3D]"
                  >
                    إغلاق
                  </button>
                </div>
              </div>
            ) : (
              <iframe
                ref={iframeRef}
                title={mode === 'preview' ? 'معاينة لعبة الحصة' : 'لعبة الحصة'}
                src={FRAME_PATH}
                className="h-full w-full border-0"
                sandbox="allow-scripts allow-same-origin"
                allow="autoplay; fullscreen"
                allowFullScreen
              />
            )}
          </div>
        </div>
      )}
    </>
  );
}

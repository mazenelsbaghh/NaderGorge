'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal, flushSync } from 'react-dom';
import { Gamepad2 } from 'lucide-react';

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

type WebkitDocument = Document & {
  webkitFullscreenElement?: Element | null;
  webkitExitFullscreen?: () => Promise<void> | void;
};

type WebkitFullscreenElement = HTMLDivElement & {
  webkitRequestFullscreen?: () => Promise<void> | void;
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
  const [scopedProgressKey, setScopedProgressKey] = useState('');
  const [viewport, setViewport] = useState<{
    top: number;
    left: number;
    width: number;
    height: number;
  } | null>(null);
  const dialogRef = useRef<HTMLDivElement>(null);
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const scopedProgressKeyRef = useRef('');

  const unlockOrientation = useCallback(() => {
    const orientation = screen.orientation as
      | (ScreenOrientation & { unlock?: () => void })
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
      // Safari keeps the body-level fullscreen overlay and asks for rotation.
    }
  }, []);

  const close = useCallback(() => {
    iframeRef.current?.contentWindow?.postMessage(
      { source: 'massar-platform', type: 'dispose' },
      window.location.origin
    );
    const webkitDocument = document as WebkitDocument;
    if (
      document.fullscreenElement === dialogRef.current ||
      webkitDocument.webkitFullscreenElement === dialogRef.current
    ) {
      const exitFullscreen =
        document.exitFullscreen?.bind(document) ||
        webkitDocument.webkitExitFullscreen?.bind(webkitDocument);
      void Promise.resolve(exitFullscreen?.()).catch(() => undefined);
    }
    unlockOrientation();
    setOpen(false);
    setFrameReady(false);
    setFrameError('');
    setScopedProgressKey('');
    scopedProgressKeyRef.current = '';
    setViewport(null);
  }, [unlockOrientation]);

  const enterNativeFullscreen = useCallback(async () => {
    const element = dialogRef.current as WebkitFullscreenElement | null;
    const requestFullscreen =
      element?.requestFullscreen?.bind(element) ||
      element?.webkitRequestFullscreen?.bind(element);
    if (!requestFullscreen) return;
    try {
      await requestFullscreen();
      await lockLandscape();
    } catch {
      // The body portal is the complete Safari fallback when native fullscreen fails.
    }
  }, [lockLandscape]);

  const openGame = useCallback(async () => {
    setFrameReady(false);
    setFrameError('');
    setScopedProgressKey('');
    scopedProgressKeyRef.current = '';
    flushSync(() => setOpen(true));
    void enterNativeFullscreen();
    try {
      const nextProgressKey = await mimGameProgressKeyForContent(
        progressKey,
        content
      );
      scopedProgressKeyRef.current = nextProgressKey;
      setScopedProgressKey(nextProgressKey);
    } catch {
      setFrameError('تعذر تجهيز حفظ التقدم على هذا الجهاز.');
    }
  }, [content, enterNativeFullscreen, progressKey]);

  useEffect(() => {
    if (!open) return;
    const previousOverflow = document.body.style.overflow;
    const previousDocumentOverflow = document.documentElement.style.overflow;
    document.body.style.overflow = 'hidden';
    document.documentElement.style.overflow = 'hidden';
    document.body.classList.add('mim-game-open');

    const updateViewport = () => {
      const visible = window.visualViewport;
      setViewport({
        top: visible?.offsetTop ?? 0,
        left: visible?.offsetLeft ?? 0,
        width: visible?.width ?? window.innerWidth,
        height: visible?.height ?? window.innerHeight,
      });
    };
    updateViewport();
    window.visualViewport?.addEventListener('resize', updateViewport);
    window.visualViewport?.addEventListener('scroll', updateViewport);
    window.addEventListener('resize', updateViewport);
    window.addEventListener('orientationchange', updateViewport);

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
            payload: {
              content,
              progressKey: scopedProgressKeyRef.current,
              mode,
            },
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
    const onFullscreenChange = () => {
      const active =
        document.fullscreenElement === dialogRef.current ||
        (document as WebkitDocument).webkitFullscreenElement ===
          dialogRef.current;
      if (!active) unlockOrientation();
    };
    window.addEventListener('message', onMessage);
    window.addEventListener('keydown', onKeyDown);
    document.addEventListener('fullscreenchange', onFullscreenChange);
    document.addEventListener('webkitfullscreenchange', onFullscreenChange);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.documentElement.style.overflow = previousDocumentOverflow;
      document.body.classList.remove('mim-game-open');
      window.visualViewport?.removeEventListener('resize', updateViewport);
      window.visualViewport?.removeEventListener('scroll', updateViewport);
      window.removeEventListener('resize', updateViewport);
      window.removeEventListener('orientationchange', updateViewport);
      window.removeEventListener('message', onMessage);
      window.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('fullscreenchange', onFullscreenChange);
      document.removeEventListener(
        'webkitfullscreenchange',
        onFullscreenChange
      );
    };
  }, [close, content, mode, open, unlockOrientation]);

  const overlay = open ? (
    <div
      ref={dialogRef}
      className="fixed z-[2147483647] flex flex-col overflow-hidden bg-[#07162d]"
      style={
        viewport
          ? {
              top: viewport.top,
              left: viewport.left,
              width: viewport.width,
              height: viewport.height,
            }
          : { inset: 0, width: '100vw', height: '100dvh' }
      }
      role="dialog"
      aria-modal="true"
      aria-label={mode === 'preview' ? 'معاينة لعبة الحصة' : 'لعبة الحصة'}
    >
      <div className="relative min-h-0 flex-1">
        {!frameReady && !frameError && (
          <div className="absolute inset-0 z-10 grid place-items-center bg-[#07162d] text-sm font-bold text-white">
            جاري تجهيز عالم ميم…
            <button
              type="button"
              onClick={close}
              className="absolute left-3 top-3 min-h-11 rounded-xl bg-white/10 px-4 text-white"
            >
              إغلاق
            </button>
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
        ) : scopedProgressKey ? (
          <iframe
            ref={iframeRef}
            title={mode === 'preview' ? 'معاينة لعبة الحصة' : 'لعبة الحصة'}
            src={FRAME_PATH}
            className="block h-full w-full border-0"
            sandbox="allow-scripts allow-same-origin"
            allow="autoplay; fullscreen"
            allowFullScreen
          />
        ) : null}
      </div>
    </div>
  ) : null;

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
      {typeof document !== 'undefined' && overlay
        ? createPortal(overlay, document.body)
        : null}
    </>
  );
}

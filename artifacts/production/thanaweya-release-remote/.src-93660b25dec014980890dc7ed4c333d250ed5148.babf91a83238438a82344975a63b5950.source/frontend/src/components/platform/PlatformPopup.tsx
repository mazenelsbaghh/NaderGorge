'use client';

import { useCallback, useEffect, useId, useState } from 'react';
import { isAxiosError } from 'axios';
import { ExternalLink, X } from 'lucide-react';

import apiClient from '@/services/api-client';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

type PlatformPopupData = {
  enabled: boolean;
  title: string;
  body: string;
  imageUrl: string;
  actionUrl: string;
  actionLabel: string;
  displayInterval: string;
  expiresAt?: string | null;
  revision: string;
};

type PopupVisitState = {
  visitsSinceDismiss: number;
};

const POPUP_VISIT_STATE_PREFIX = 'massar-platform-popup-visits:';
const LEGACY_POPUP_DISMISSAL_PREFIX = 'massar-platform-popup-dismissed:';

function getDisplayInterval(rawInterval: string) {
  const displayInterval = Number(rawInterval);
  return Number.isInteger(displayInterval) && displayInterval > 0 ? displayInterval : 0;
}

function getPopupVisitState(revision: string): PopupVisitState | null {
  const storedState = window.localStorage.getItem(`${POPUP_VISIT_STATE_PREFIX}${revision}`);
  if (!storedState) {
    return window.localStorage.getItem(`${LEGACY_POPUP_DISMISSAL_PREFIX}${revision}`) === 'true'
      ? { visitsSinceDismiss: 0 }
      : null;
  }

  try {
    const parsedState = JSON.parse(storedState) as Partial<PopupVisitState>;
    return typeof parsedState.visitsSinceDismiss === 'number' && parsedState.visitsSinceDismiss >= 0
      ? { visitsSinceDismiss: parsedState.visitsSinceDismiss }
      : null;
  } catch (error) {
    if (error instanceof SyntaxError) return null;
    throw error;
  }
}

function setPopupVisitState(revision: string, visitsSinceDismiss: number) {
  window.localStorage.setItem(
    `${POPUP_VISIT_STATE_PREFIX}${revision}`,
    JSON.stringify({ visitsSinceDismiss }),
  );
}

function isSafeActionUrl(actionUrl: string) {
  if (actionUrl.startsWith('/')) return true;

  try {
    const url = new URL(actionUrl);
    return url.protocol === 'https:' || url.protocol === 'http:';
  } catch (error) {
    if (error instanceof TypeError) return false;
    throw error;
  }
}

function formatRemaining(expiresAt: string | null | undefined, now: number) {
  if (!expiresAt) return null;
  const remainingSeconds = Math.max(0, Math.floor((new Date(expiresAt).getTime() - now) / 1000));
  if (remainingSeconds <= 0) return null;
  const days = Math.floor(remainingSeconds / 86_400);
  const hours = Math.floor((remainingSeconds % 86_400) / 3_600);
  const minutes = Math.floor((remainingSeconds % 3_600) / 60);
  const seconds = remainingSeconds % 60;
  if (days > 0) return `متبقي ${days} يوم و${hours} ساعة`;
  if (hours > 0) return `متبقي ${hours} ساعة و${minutes} دقيقة`;
  return `متبقي ${minutes} دقيقة و${seconds} ثانية`;
}

export function PlatformPopup() {
  const titleId = useId();
  const descriptionId = useId();
  const [popup, setPopup] = useState<PlatformPopupData | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [now, setNow] = useState(() => Date.now());

  const dismiss = useCallback(() => {
    if (popup) {
      setPopupVisitState(popup.revision, 0);
    }
    setIsOpen(false);
  }, [popup]);

  useEffect(() => {
    let mounted = true;

    async function loadPopup() {
      let platformPopup: PlatformPopupData;
      try {
        platformPopup = (await apiClient.get<PlatformPopupData>('/public/popup')).data;
      } catch (error) {
        if (isAxiosError(error)) return;
        throw error;
      }

      if (!platformPopup.enabled || !platformPopup.title.trim()) return;

      const visitState = getPopupVisitState(platformPopup.revision);
      const displayInterval = getDisplayInterval(platformPopup.displayInterval);
      if (visitState && displayInterval === 0) return;
      if (visitState && displayInterval > 0) {
        const nextVisitCount = visitState.visitsSinceDismiss + 1;
        if (nextVisitCount < displayInterval) {
          setPopupVisitState(platformPopup.revision, nextVisitCount);
          return;
        }
      }

      if (mounted) {
        setPopup(platformPopup);
        setIsOpen(true);
      }
    }

    void loadPopup();
    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    if (!isOpen) return;

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') dismiss();
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [dismiss, isOpen]);

  useEffect(() => {
    if (!popup?.expiresAt) return;
    const timer = window.setInterval(() => {
      const currentTime = Date.now();
      setNow(currentTime);
      if (new Date(popup.expiresAt as string).getTime() <= currentTime) setIsOpen(false);
    }, 1000);
    return () => window.clearInterval(timer);
  }, [popup?.expiresAt]);

  if (!popup || !isOpen) return null;

  const actionUrl = popup.actionUrl.trim();
  const hasAction = Boolean(actionUrl && isSafeActionUrl(actionUrl));

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-[#091a35]/70 p-4 backdrop-blur-sm"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) dismiss();
      }}
    >
      <section
        dir="rtl"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={popup.body.trim() ? descriptionId : undefined}
        className="relative w-full max-w-lg overflow-hidden rounded-[24px] border border-[#dce1e6] bg-[#f6f7f8] text-[#0a1d3d] shadow-[0_28px_80px_rgba(10,29,61,0.34)]"
      >
        <button
          type="button"
          onClick={dismiss}
          className="absolute left-3 top-3 z-10 inline-flex h-10 w-10 items-center justify-center rounded-full border border-[#dce1e6] bg-[#f6f7f8]/95 text-[#0a1d3d] transition hover:bg-[#eef1f4] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0e8f8f]"
          aria-label="إغلاق النافذة"
        >
          <X className="h-5 w-5" />
        </button>

        {popup.imageUrl.trim() && (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={resolveMediaUrl(popup.imageUrl)}
            alt=""
            className="max-h-72 w-full object-cover"
          />
        )}

        <div className="px-6 pb-6 pt-7 sm:px-8 sm:pb-8">
          <h2 id={titleId} className="max-w-[17ch] text-2xl font-black leading-tight text-[#0a1d3d] sm:text-3xl">
            {popup.title}
          </h2>
          {popup.body.trim() && (
            <p id={descriptionId} className="mt-3 whitespace-pre-line text-base leading-7 text-[#2e3a47]">
              {popup.body}
            </p>
          )}

          {formatRemaining(popup.expiresAt, now) && (
            <div className="mt-4 inline-flex items-center rounded-full bg-[#0e8f8f]/10 px-3 py-1.5 text-sm font-black text-[#0e6f6f]">
              {formatRemaining(popup.expiresAt, now)}
            </div>
          )}

          <div className="mt-6 flex flex-wrap items-center gap-3">
            {hasAction && (
              <a
                href={actionUrl}
                target={actionUrl.startsWith('/') ? undefined : '_blank'}
                rel={actionUrl.startsWith('/') ? undefined : 'noreferrer'}
                onClick={dismiss}
                className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#0a1d3d] px-5 text-sm font-bold text-white transition hover:bg-[#12305f] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0e8f8f] focus-visible:ring-offset-2"
              >
                {popup.actionLabel.trim() || 'فتح الرابط'}
                <ExternalLink className="h-4 w-4" />
              </a>
            )}
            <button
              type="button"
              onClick={dismiss}
              className="min-h-11 rounded-xl px-4 text-sm font-bold text-[#0e6f6f] transition hover:bg-[#e5f4f3] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0e8f8f]"
            >
              {hasAction ? 'لاحقاً' : 'فهمت'}
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}

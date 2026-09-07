"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { isAxiosError } from "axios";
import { contentService, type LessonDetailDto } from "@/services/content-service";

type LessonSnapshot = {
  lessonId: string;
  lesson: LessonDetailDto | null;
  error: string;
};

export function useLessonDetail(lessonId: string) {
  const [snapshot, setSnapshot] = useState<LessonSnapshot>({ lessonId, lesson: null, error: "" });
  const requestRef = useRef<AbortController | null>(null);

  const fetchLessonDetail = useCallback(async () => {
    requestRef.current?.abort();
    const request = new AbortController();
    requestRef.current = request;
    setSnapshot(previous => ({
      lessonId,
      lesson: previous.lessonId === lessonId ? previous.lesson : null,
      error: "",
    }));

    try {
      const response = await contentService.getLessonDetail(lessonId, request.signal);
      if (request.signal.aborted) return;
      setSnapshot({
        lessonId,
        lesson: response.data.data ?? null,
        error: response.data.data ? "" : "تعذر تحميل بيانات الدرس الآن.",
      });
    } catch (error: unknown) {
      if (request.signal.aborted) return;
      const status = isAxiosError(error) ? error.response?.status : undefined;
      const canKeepPlayback = isAxiosError(error)
        && (status === undefined || status === 408 || status === 429 || status >= 500);
      const message = status === 403 || status === 401
        ? "هذا الدرس غير متاح لحسابك حاليًا."
        : status === 404
          ? "هذا الدرس لم يعد موجودًا في المحتوى."
          : "تعذر تحديث بيانات الدرس الآن. حاول مرة أخرى.";
      // A failed background read must not destroy the active playback session.
      // Definitive access failures still remove protected content immediately.
      setSnapshot(previous => ({
        lessonId,
        lesson: canKeepPlayback && previous.lessonId === lessonId ? previous.lesson : null,
        error: message,
      }));
    }
  }, [lessonId]);

  useEffect(() => {
    void fetchLessonDetail();
    return () => requestRef.current?.abort();
  }, [fetchLessonDetail]);

  const lesson = snapshot.lessonId === lessonId ? snapshot.lesson : null;
  useEffect(() => {
    const refresh = () => { void fetchLessonDetail(); };
    window.addEventListener('massar:watch-registered', refresh);
    return () => window.removeEventListener('massar:watch-registered', refresh);
  }, [fetchLessonDetail]);
  const error = snapshot.lessonId === lessonId ? snapshot.error : "";
  return {
    lesson,
    loading: !lesson && !error,
    error: lesson ? "" : error,
    refreshError: lesson ? error : "",
    fetchLessonDetail,
  };
}

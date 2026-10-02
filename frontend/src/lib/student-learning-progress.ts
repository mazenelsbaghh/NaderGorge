import type { MyLessonDto } from '../services/student-service';

export type LearningVideo = { durationSeconds?: number | null; learningWatchedSeconds?: number };

function watchProgressPercentage(watchedSeconds: number, durationSeconds: number): number {
  const percent = Math.min(100, Math.max(0, watchedSeconds) * 100 / durationSeconds);
  const nearestInteger = Math.round(percent);
  // Ignore floating-point noise at an exact boundary, but floor real partial percentages.
  return Math.abs(percent - nearestInteger) <= Number.EPSILON * Math.max(1, percent) * 2
    ? nearestInteger
    : Math.floor(percent);
}

export function isVideoLearningComplete(video: LearningVideo): boolean {
  const duration = video.durationSeconds;
  return duration != null && Number.isFinite(duration) && duration > 0
    && (video.learningWatchedSeconds ?? 0) >= duration - Math.min(2, duration * 0.01);
}

export function videoProgressPercent(video: LearningVideo): number | null {
  const duration = video.durationSeconds;
  if (duration == null || !Number.isFinite(duration) || duration <= 0) return null;
  if (isVideoLearningComplete(video)) return 100;
  return watchProgressPercentage(video.learningWatchedSeconds ?? 0, duration);
}

export function lessonProgressPercent(videos: LearningVideo[]): number | null {
  if (videos.length === 0 || videos.some(video => videoProgressPercent(video) === null)) return null;
  const duration = videos.reduce((sum, video) => sum + video.durationSeconds!, 0);
  const watched = videos.reduce((sum, video) => sum + (isVideoLearningComplete(video) ? video.durationSeconds! : Math.min(video.durationSeconds!, Math.max(0, video.learningWatchedSeconds ?? 0))), 0);
  return watchProgressPercentage(watched, duration);
}

export function learningSummary(lessons: MyLessonDto[]) {
  const videos = lessons.filter(lesson => lesson.videoCount > 0);
  const durationKnown = videos.length > 0 && videos.every(lesson => (lesson.totalVideoSeconds ?? 0) > 0);
  const duration = videos.reduce((sum, lesson) => sum + (lesson.totalVideoSeconds ?? 0), 0);
  const watched = videos.reduce((sum, lesson) => sum + (lesson.isCompleted ? (lesson.totalVideoSeconds ?? 0) : Math.min(lesson.recordedWatchSeconds ?? 0, lesson.totalVideoSeconds ?? 0)), 0);
  return {
    percent: durationKnown ? watchProgressPercentage(watched, duration) : null,
    completedLessons: lessons.filter(lesson => lesson.isCompleted).length,
    completedVideos: lessons.reduce((sum, lesson) => sum + (lesson.watchedVideoCount ?? 0), 0),
    totalVideos: lessons.reduce((sum, lesson) => sum + lesson.videoCount, 0),
  };
}

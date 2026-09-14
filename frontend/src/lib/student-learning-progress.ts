import type { MyLessonDto } from '../services/student-service';

export type LearningVideo = { durationSeconds?: number | null; learningWatchedSeconds?: number };

export function videoProgressPercent(video: LearningVideo): number | null {
  const duration = video.durationSeconds;
  if (duration == null || !Number.isFinite(duration) || duration <= 0) return null;
  return Math.floor(Math.min(100, Math.max(0, video.learningWatchedSeconds ?? 0) / duration * 100));
}

export function lessonProgressPercent(videos: LearningVideo[]): number | null {
  if (videos.length === 0 || videos.some(video => videoProgressPercent(video) === null)) return null;
  const duration = videos.reduce((sum, video) => sum + video.durationSeconds!, 0);
  const watched = videos.reduce((sum, video) => sum + Math.min(video.durationSeconds!, Math.max(0, video.learningWatchedSeconds ?? 0)), 0);
  return Math.floor(watched / duration * 100);
}

export function learningSummary(lessons: MyLessonDto[]) {
  const videos = lessons.filter(lesson => lesson.videoCount > 0);
  const durationKnown = videos.length > 0 && videos.every(lesson => (lesson.totalVideoSeconds ?? 0) > 0);
  const duration = videos.reduce((sum, lesson) => sum + (lesson.totalVideoSeconds ?? 0), 0);
  const watched = videos.reduce((sum, lesson) => sum + Math.min(lesson.recordedWatchSeconds ?? 0, lesson.totalVideoSeconds ?? 0), 0);
  return {
    percent: durationKnown ? Math.floor(Math.min(100, watched / duration * 100)) : null,
    completedLessons: lessons.filter(lesson => lesson.isCompleted).length,
    completedVideos: lessons.reduce((sum, lesson) => sum + (lesson.watchedVideoCount ?? 0), 0),
    totalVideos: lessons.reduce((sum, lesson) => sum + lesson.videoCount, 0),
  };
}

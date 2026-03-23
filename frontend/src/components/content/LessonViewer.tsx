'use client';

import { LessonDetailDto } from '@/services/content-service';
import { useRouter } from 'next/navigation';
import { VideoPlayer } from './VideoPlayer';

export function LessonViewer({ lesson, packageId }: { lesson: LessonDetailDto, packageId?: string }) {
  const router = useRouter();

  return (
    <div className="space-y-8">
      <div className="rounded-2xl border border-gray-200 bg-white/50 p-6 shadow-lg backdrop-blur-sm dark:border-gray-800 dark:bg-gray-900/50">
        <h1 className="text-3xl font-bold text-gray-900 dark:text-white">{lesson.title}</h1>
        <p className="mt-3 text-lg leading-relaxed text-gray-600 dark:text-gray-300">{lesson.summary}</p>
      </div>

      <div className="grid gap-6 md:grid-cols-3">
        {/* Main Content Area */}
        <div className="md:col-span-2 space-y-6">
          {lesson.videos.map((video) => (
            <div key={video.id} className="space-y-3">
              <h3 className="text-xl font-semibold text-gray-800 dark:text-gray-200">{video.title}</h3>
              <VideoPlayer video={video} onLockInfo={(msg) => alert(msg)} />
            </div>
          ))}
          {lesson.videos.length === 0 && (
            <div className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-gray-500 dark:border-gray-700">
              No videos available for this lesson.
            </div>
          )}
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          <div className="rounded-2xl border border-gray-200 bg-white p-6 shadow-md dark:border-gray-800 dark:bg-gray-900">
            <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-4">Resources</h3>
            <ul className="space-y-3">
              {lesson.resources.map((res) => (
                <li key={res.id}>
                  <a
                    href={res.fileUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center gap-3 rounded-xl bg-blue-50 px-4 py-3 text-sm font-medium text-blue-700 transition-colors hover:bg-blue-100 dark:bg-blue-900/20 dark:text-blue-400 dark:hover:bg-blue-900/40"
                  >
                    <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                    </svg>
                    {res.title}
                  </a>
                </li>
              ))}
              {lesson.resources.length === 0 && (
                <p className="text-sm text-gray-500 dark:text-gray-400">No resources available.</p>
              )}
            </ul>
          </div>

          {lesson.examId && (
            <div className="rounded-2xl bg-gradient-to-br from-indigo-500 to-purple-600 p-6 text-white shadow-lg">
              <h3 className="text-lg font-bold">Lesson Exam</h3>
              <p className="mt-2 text-sm text-indigo-100">Test your knowledge on this lesson.</p>
              <button 
                onClick={() => router.push(`/student/exams/${lesson.examId}?packageId=${packageId}`)}
                className="mt-4 w-full rounded-xl bg-white px-4 py-2 text-sm font-bold text-indigo-600 shadow-md hover:bg-indigo-50 transition-colors"
              >
                Take Exam
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

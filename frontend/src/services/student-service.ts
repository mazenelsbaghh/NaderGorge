import apiClient from './api-client';

export interface ActivePackageDto {
  id: string;
  name: string;
  description: string;
  lessonsCompleted: number;
  totalLessons: number;
  progressPercent: number;
}

export interface ResumePointDto {
  packageId: string;
  packageName: string;
  lessonId: string;
  lessonTitle: string;
  lessonOrder: number;
}

export interface UpcomingExamDto {
  examId: string;
  examTitle: string;
  lessonTitle: string;
}

export interface DashboardDto {
  studentName: string;
  activePackages: ActivePackageDto[];
  resumePoint?: ResumePointDto;
  upcomingExams: UpcomingExamDto[];
  overallProgressPercent: number;
  totalLessonsCompleted: number;
  totalLessons: number;
  codesRedeemed: number;
}

export interface LessonProgressItemDto {
  id: string;
  title: string;
  order: number;
  isCompleted: boolean;
  isLocked: boolean;
  hasExam: boolean;
  examPassed: boolean;
}

export interface PackageProgressDto {
  id: string;
  name: string;
  lessons: LessonProgressItemDto[];
}

export interface ProgressDto {
  packages: PackageProgressDto[];
  totalLessons: number;
  completedLessons: number;
  overallPercent: number;
  examsPassed: number;
  examsFailed: number;
}

export const studentService = {
  getDashboard: async (): Promise<DashboardDto> => {
    const res = await apiClient.get('/api/student/dashboard');
    return res.data?.data;
  },

  getProgress: async (): Promise<ProgressDto> => {
    const res = await apiClient.get('/api/student/progress');
    return res.data?.data;
  }
};

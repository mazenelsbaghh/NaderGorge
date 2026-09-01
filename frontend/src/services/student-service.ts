import apiClient from './api-client';
import { invalidateMany } from '@/lib/cache-invalidation';

export interface ActivePackageDto {
  id: string;
  name: string;
  description: string;
  lessonsCompleted: number;
  totalLessons: number;
  progressPercent: number;
  imageUrl?: string;
  teacherId: string;
  teacherName: string;
  teacherProfileImageUrl?: string;
  subjectId: string;
  subjectName: string;
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

export interface UpcomingHomeworkDto {
  homeworkId: string;
  homeworkTitle: string;
  lessonTitle: string;
}

export interface DashboardDto {
  studentName: string;
  activePackages: ActivePackageDto[];
  resumePoint?: ResumePointDto;
  upcomingExams: UpcomingExamDto[];
  upcomingHomeworks: UpcomingHomeworkDto[];
  overallProgressPercent: number;
  totalLessonsCompleted: number;
  totalLessons: number;
  codesRedeemed: number;
  avatarSlug?: string | null;
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

export interface MyLessonDto {
  id: string;
  title: string;
  order: number;
  packageId: string;
  packageName: string;
  termTitle: string;
  sectionTitle: string;
  teacherName: string;
  imageUrl?: string | null;
  isCompleted: boolean;
  videoCount: number;
}

export interface QuickAccessItemDto {
  title: string;
  pathBreadcrumb: string;
  url: string;
  accessType: number | 'Term' | 'Month' | 'Lesson' | 'Video'; // API may serialize enums as names.
  packageId?: string;
  parentUrl?: string;
  imageUrl?: string;
  teacherName?: string;
  teacherProfileImageUrl?: string;
  badge?: string;
}

export interface ExamMistakeItemDto {
  examQuestionId: string;
  questionOrder: number;
  questionText: string;
  yourAnswer?: string;
  correctAnswer?: string;
  timesMissed: number;
  lastMissedAt: string;
  canRevealCorrectAnswer: boolean;
}

export interface ExamMistakeGroupDto {
  examId: string;
  examTitle: string;
  packageId?: string;
  packageName: string;
  lessonId?: string;
  lessonTitle: string;
  passedEventually: boolean;
  lastMistakeAt: string;
  mistakesCount: number;
  latestScore?: number;
  latestTotalScore?: number;
  latestEvaluation?: string;
  items: ExamMistakeItemDto[];
}

export interface HomeworkWeaknessDto {
  homeworkId: string;
  homeworkTitle: string;
  packageId?: string;
  packageName: string;
  lessonId: string;
  lessonTitle: string;
  score: number;
  passingScore?: number;
  status: string;
  assistantNotes?: string;
}

export interface StudentMistakesDto {
  totalExamMistakes: number;
  examsWithMistakes: number;
  weakHomeworkCount: number;
  examMistakes: ExamMistakeGroupDto[];
  homeworkWeaknesses: HomeworkWeaknessDto[];
}

export interface ThemePaletteOptionDto {
  id: string;
  name: string;
  mode: 'light' | 'dark';
  previewAccent: string;
}

export interface StudentThemePreferencesDto {
  currentMode: 'light' | 'dark';
  selectedLightPaletteId: string;
  selectedDarkPaletteId: string;
  avatarSlug?: string | null;
  defaultLightPaletteId: string;
  defaultDarkPaletteId: string;
  availableLightPalettes: ThemePaletteOptionDto[];
  availableDarkPalettes: ThemePaletteOptionDto[];
}

export interface PublicTeacherDto {
  id: string;
  teacherId?: string;
  slug?: string;
  fullName: string;
  displayName?: string;
  bio: string;
  specialization: string;
  profileImageUrl?: string;
  introVideoUrl?: string;
  contactInfo?: string;
  assistantPhoneNumbers?: string;
  facebookUrl?: string;
  youtubeUrl?: string;
  telegramUrl?: string;
  ratingAverage?: number;
  ratingCount?: number;
  subjectNames: string[];
}

export interface PublicTeacherContentDto {
  id: string;
  name?: string;
  title?: string;
  price?: number;
  imageUrl?: string | null;
}

export interface PublicTeacherDetailDto extends PublicTeacherDto {
  subjects: Array<{ id?: string; subjectId?: string; name: string }>;
  packages: PublicTeacherContentDto[];
  sharedPackages: PublicTeacherContentDto[];
  lessons: PublicTeacherContentDto[];
}

export interface PublicPackageDetailDto {
  id: string;
  name: string;
  description: string;
  price: number;
  imageUrl?: string | null;
  subjectName: string;
  teacherName: string;
  teacherId: string;
  terms: Array<{
    id: string;
    title: string;
    price: number;
    imageUrl?: string | null;
    sections: Array<{
      id: string;
      title: string;
      lessons: Array<{ id: string; title: string }>;
    }>;
  }>;
}

export interface TeacherCommunityPostDto {
  id: string;
  body: string;
  createdAt: string;
  authorName: string;
}

export interface StudentProfileDto {
  userId: string;
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string;
  gender: string;
  governorate: string;
  district: string | null;
  address: string;
  secondaryPhone: string | null;
  parentPhone: string | null;
  secondaryParentPhone: string | null;
  motherPhone: string | null;
  schoolName: string | null;
  educationStage: string;
  gradeLevel: string;
  studyTrack: string | null;
  deviceCount: number;
  maxDevices: number;
}

export interface UpdateStudentProfileDto {
  fullName: string;
  address: string;
  secondaryPhone?: string | null;
  parentPhone?: string | null;
  secondaryParentPhone?: string | null;
  motherPhone?: string | null;
  schoolName?: string | null;
  educationStage: string;
  gradeLevel: string;
  studyTrack?: string | null;
}

export interface StudentNotificationDto {
  id: string;
  title: string;
  body: string;
  isRead: boolean;
  createdAt: string;
}

export interface StudentGamificationDto {
  totalPoints: number;
  currentStreakCount: number;
  longestStreakCount: number;
  levelName: string;
}

export interface ShellBootstrapDto {
  unreadNotificationsCount: number;
  currentBalance: number;
  promotionalBalance: number;
  gamification: StudentGamificationDto;
  themePreferences: StudentThemePreferencesDto;
  avatarSlug?: string | null;
  parentTrackingCode?: string;
  hasSeenTrackingCodePopup?: boolean;
}

export const studentService = {
  getDashboard: async (signal?: AbortSignal): Promise<DashboardDto> => {
    const res = await apiClient.get('/student/dashboard', { signal });
    return res.data?.data;
  },

  getShellBootstrap: async (): Promise<ShellBootstrapDto> => {
    const res = await apiClient.get('/student/shell-bootstrap');
    return res.data?.data;
  },

  getPublicTeachers: async (signal?: AbortSignal): Promise<PublicTeacherDto[]> => {
    const res = await apiClient.get('/public/teachers', { signal, suppressErrorToast: true });
    return res.data?.data || [];
  },

  getLandingTeachers: async (): Promise<PublicTeacherDto[]> => {
    const res = await apiClient.get('/public/teachers/landing', { suppressErrorToast: true });
    return res.data?.data || [];
  },

  getPublicTeacherDetail: async (teacherIdOrSlug: string): Promise<PublicTeacherDetailDto> => {
    const res = await apiClient.get(`/public/teachers/${teacherIdOrSlug}`);
    return res.data?.data;
  },

  getPublicPackage: async (packageId: string): Promise<PublicPackageDetailDto> => {
    const res = await apiClient.get(`/public/packages/${packageId}`);
    return res.data?.data;
  },

  getTeacherCommunityPosts: async (teacherId: string): Promise<TeacherCommunityPostDto[]> => {
    const res = await apiClient.get(`/public/teachers/${teacherId}/community-posts`);
    return res.data?.data || [];
  },

  createTeacherCommunityPost: async (teacherId: string, body: string): Promise<{ id: string }> => {
    const res = await apiClient.post(`/public/teachers/${teacherId}/community-posts`, { body });
    return res.data?.data;
  },

  getQuickAccess: async (signal?: AbortSignal): Promise<QuickAccessItemDto[]> => {
    const res = await apiClient.get('/student/dashboard/quick-access', { signal });
    return res.data?.data || [];
  },

  getProgress: async (): Promise<ProgressDto> => {
    const res = await apiClient.get('/student/progress');
    return res.data?.data;
  },

  getMyLessons: async (signal?: AbortSignal): Promise<MyLessonDto[]> => {
    const res = await apiClient.get('/student/lessons', { signal });
    return res.data?.data || [];
  },

  getMistakes: async (): Promise<StudentMistakesDto> => {
    const res = await apiClient.get('/student/mistakes');
    return res.data?.data;
  },

  getThemePreferences: async (): Promise<StudentThemePreferencesDto> => {
    const res = await apiClient.get('/student/theme-preferences');
    return res.data?.data;
  },

  updateThemePreferences: async (payload: {
    lightPaletteId: string;
    darkPaletteId: string;
    currentMode: 'light' | 'dark';
    avatarSlug?: string | null;
  }): Promise<StudentThemePreferencesDto> => {
    const res = await apiClient.put('/student/theme-preferences', payload);
    return res.data?.data;
  },

  getProfile: async (): Promise<StudentProfileDto> => {
    const res = await apiClient.get('/student/profile');
    return res.data?.data;
  },

  updateProfile: async (payload: UpdateStudentProfileDto): Promise<void> => {
    const res = await apiClient.put('/student/profile', payload);
    return res.data?.data;
  },

  getNotifications: async (): Promise<StudentNotificationDto[]> => {
    const res = await apiClient.get('/student/notifications');
    return res.data?.data || [];
  },

  markNotificationAsRead: async (id: string): Promise<void> => {
    const res = await apiClient.post(`/student/notifications/${id}/read`);
    invalidateMany(['notifications', 'student:shell']);
    return res.data?.data;
  },

  uploadAudio: async (file: File): Promise<{ url: string }> => {
    const formData = new FormData();
    formData.append('audio', file);
    const res = await apiClient.post('/student/upload-audio', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return res.data?.data;
  },

  acknowledgeTrackingPopup: async (): Promise<void> => {
    await apiClient.post('/student/acknowledge-tracking-popup');
  }
};

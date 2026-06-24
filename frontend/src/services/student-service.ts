import apiClient from './api-client';

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

export interface DashboardDto {
  studentName: string;
  activePackages: ActivePackageDto[];
  resumePoint?: ResumePointDto;
  upcomingExams: UpcomingExamDto[];
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

export interface QuickAccessItemDto {
  title: string;
  pathBreadcrumb: string;
  url: string;
  accessType: number; // 1 = Term, 2 = Month, 3 = Lesson
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
  defaultLightPaletteId: string;
  defaultDarkPaletteId: string;
  availableLightPalettes: ThemePaletteOptionDto[];
  availableDarkPalettes: ThemePaletteOptionDto[];
}

export interface PublicTeacherDto {
  id: string;
  fullName: string;
  bio: string;
  specialization: string;
  profileImageUrl?: string;
  subjectNames: string[];
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
  address: string;
  secondaryPhone?: string | null;
  parentPhone?: string | null;
  secondaryParentPhone?: string | null;
  motherPhone?: string | null;
  schoolName?: string | null;
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
  gamification: StudentGamificationDto;
  themePreferences: StudentThemePreferencesDto;
  avatarSlug?: string | null;
  parentTrackingCode?: string;
  hasSeenTrackingCodePopup?: boolean;
}

export const studentService = {
  getDashboard: async (): Promise<DashboardDto> => {
    const res = await apiClient.get('/student/dashboard');
    return res.data?.data;
  },

  getShellBootstrap: async (): Promise<ShellBootstrapDto> => {
    const res = await apiClient.get('/student/shell-bootstrap');
    return res.data?.data;
  },

  getPublicTeachers: async (): Promise<PublicTeacherDto[]> => {
    const res = await apiClient.get('/public/teachers');
    return res.data?.data || [];
  },

  getQuickAccess: async (): Promise<QuickAccessItemDto[]> => {
    const res = await apiClient.get('/student/dashboard/quick-access');
    return res.data?.data || [];
  },

  getProgress: async (): Promise<ProgressDto> => {
    const res = await apiClient.get('/student/progress');
    return res.data?.data;
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


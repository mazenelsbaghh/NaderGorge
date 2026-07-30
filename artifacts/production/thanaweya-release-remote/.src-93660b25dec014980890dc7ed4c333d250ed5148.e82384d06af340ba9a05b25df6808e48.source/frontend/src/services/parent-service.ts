import axios from 'axios';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5245/api';

export type ParentAcademicDetails = {
  studentName: string;
  grade: string;
  school?: string | null;
  attendance: { totalLessons: number; watchedLessons: number; completionRate: number };
  exams: Array<{ examId: string; examTitle: string; packageName: string; termTitle: string; percentage: number; submittedAt?: string | null; status: string }>;
  homeworks: Array<{ homeworkId: string; title: string; packageName: string; teacherName: string; isSubmitted: boolean; grade?: string | null; submittedAt?: string | null }>;
  warnings: Array<{ reason: string; severity: string; createdAt: string }>;
  teachers: Array<{ teacherId: string; teacherName: string; specialization?: string | null }>;
  watchLessons: Array<{ lessonId: string; lessonTitle: string; packageName: string; termTitle: string; watchedVideos: number; totalVideos: number; isCompleted: boolean; lastWatchedAt?: string | null }>;
  balance: { currentBalance: number };
  courses: Array<{ packageId: string; packageName: string; teacherName: string; terms: Array<{ termId: string; termTitle: string; lessonCount: number; examCount: number }> }>;
};

type ApiResponse<T> = { success: boolean; message?: string; data?: T };

export const parentService = {
  async verifyCode(trackingCode: string) {
    const { data } = await axios.post<ApiResponse<{ token: string; studentName: string }>>(
      `${API_BASE_URL}/parent/verify-code`,
      { trackingCode, platform: 'web' },
    );
    if (!data.success || !data.data) throw new Error(data.message || 'تعذر التحقق من رمز المتابعة.');
    return data.data;
  },

  async getStudentDetails(token: string) {
    const { data } = await axios.get<ApiResponse<ParentAcademicDetails>>(
      `${API_BASE_URL}/parent/student-details`,
      { headers: { Authorization: `Bearer ${token}` } },
    );
    if (!data.success || !data.data) throw new Error(data.message || 'تعذر تحميل بيانات المتابعة.');
    return data.data;
  },
};

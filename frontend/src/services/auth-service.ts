import apiClient from './api-client';

export interface RegisterData {
  fullName: string;
  phoneNumber: string;
  password: string;
  studentCode: string;
  dateOfBirth: string;
  gender: 'Male' | 'Female';
  governorate: string;
  address: string;
  parentPhone: string;
  isFatherAlive: boolean;
  isMotherAlive: boolean;
  educationStage: 'Secondary' | 'Baccalaureate';
  gradeLevel: string;
  studyTrack?: string;
}

export interface LoginData {
  phoneNumber: string;
  password: string;
  deviceFingerprint: string;
  deviceName?: string;
}

export interface CompleteProfileData {
  parentPhone: string;
  governorate: string;
  city: string;
  school: string;
}

export const authService = {
  register: (data: RegisterData) => apiClient.post('/auth/register', data),
  login: (data: LoginData) => apiClient.post('/auth/login', data),
  refresh: (refreshToken: string) => apiClient.post('/auth/refresh', { refreshToken }),
  completeProfile: (data: CompleteProfileData) => apiClient.post('/auth/complete-profile', data),
  activateCode: (code: string) => apiClient.post('/codes/activate', { code }),
};

export function getDeviceFingerprint(): string {
  if (typeof window === 'undefined') return 'ssr';
  let fp = localStorage.getItem('deviceFingerprint');
  if (!fp) {
    fp = crypto.randomUUID();
    localStorage.setItem('deviceFingerprint', fp);
  }
  return fp;
}

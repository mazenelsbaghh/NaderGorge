import apiClient from './api-client';

export interface RegisterData {
  // ── Personal ───────────────────────────────────────────────────────────
  fullName: string;
  phoneNumber: string;
  secondaryPhone?: string;
  password: string;
  dateOfBirth: string;
  gender: 'Male' | 'Female';
  nationality?: string;                    // NEW: Arab nationality
  governorate: string;
  district: string;
  address: string;

  // ── Parent ─────────────────────────────────────────────────────────────
  parentPhone?: string;                    // Father's phone — optional if father deceased
  secondaryParentPhone?: string;
  motherPhone?: string;                    // NEW: Mother's phone
  isFatherAlive: boolean;
  isMotherAlive: boolean;
  fatherDateOfBirth?: string;              // NEW: Father's date of birth (ISO string)
  motherDateOfBirth?: string;              // NEW: Mother's date of birth (ISO string)

  // ── School ─────────────────────────────────────────────────────────────
  schoolName?: string;                     // NEW: School name (free text)
  schoolType?: string;                     // NEW: School type enum string

  // ── Academic ───────────────────────────────────────────────────────────
  educationStage: 'Secondary' | 'Baccalaureate' | 'Primary' | 'Preparatory' | 'Azhari' | 'American';
  gradeLevel: string;
  studyTrack?: string;
  avatarSlug?: string;
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

export interface VerifyResetFieldsData {
  phoneNumber: string;
  dateOfBirth: string;
  governorate: string;
  district: string;
}

export interface ResetPasswordData {
  token: string;
  newPassword: string;
}

export const authService = {
  register: (data: RegisterData) => apiClient.post('/auth/register', data),
  login: (data: LoginData) => apiClient.post('/auth/login', data),
  refresh: () => apiClient.post('/auth/refresh', {}),
  logout: () => apiClient.post('/auth/logout', {}),
  completeProfile: (data: CompleteProfileData) => apiClient.post('/auth/complete-profile', data),
  activateCode: (code: string) => apiClient.post('/codes/activate', { code }),
  validateCode: (code: string) => apiClient.get(`/codes/validate/${code}`),
  verifyResetFields: (data: VerifyResetFieldsData) => apiClient.post('/auth/verify-reset-fields', data),
  resetPassword: (data: ResetPasswordData) => apiClient.post('/auth/reset-password', data),
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

import apiClient from './api-client';

export interface ApiResponse<T = any> {
  data: T;
  success: boolean;
  message: string;
}

export interface EmployeeProfileDto {
  id: string;
  userId: string;
  basicSalary: number;
  standardStartTime: string; // "hh:mm:ss" or "hh:mm"
  targetDailyHours: number;
}

export interface EmployeeDto {
  id: string;
  userId: string;
  fullName: string;
  phoneNumber: string;
  roles: string[];
  employeeProfile?: EmployeeProfileDto;
}

export interface SaveEmployeeProfilePayload {
  userId: string;
  basicSalary: number;
  standardStartTime: string;
  targetDailyHours: number;
}

export interface AttendanceLogDto {
  id: string;
  date: string; // "yyyy-MM-dd"
  clockIn: string; // DateTime ISO
  clockOut?: string; // DateTime ISO
  lateMinutes: number;
  status: string; // "Present", "Late", "Absent", "Sick", "Leave"
  ipAddress: string;
  userAgent: string;
  durationMinutes?: number;
}

export interface AdminAttendanceLogDto extends AttendanceLogDto {
  employeeId: string;
  employeeName: string;
  employeePhone: string;
}

export interface VacationDto {
  id: string;
  startDate: string; // "yyyy-MM-dd"
  endDate: string; // "yyyy-MM-dd"
  status: string; // "Pending", "Approved", "Rejected"
  reason: string;
  handledByName?: string;
  handledAt?: string;
}

export interface AdminVacationDto extends VacationDto {
  employeeId: string;
  employeeName: string;
  employeePhone: string;
}

export const hrService = {
  // US1: Employee Profile Setup & Management
  listEmployees: async (search?: string): Promise<EmployeeDto[]> => {
    const res = await apiClient.get<ApiResponse<EmployeeDto[]>>(
      '/admin/hr/employees',
      {
        params: search ? { search } : {},
      }
    );
    return res.data?.data ?? [];
  },

  saveEmployeeProfile: async (
    payload: SaveEmployeeProfilePayload
  ): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>(
      '/admin/hr/employees',
      payload
    );
    return res.data;
  },

  // US2: Employee Attendance Logging (Clock-in/out)
  clockIn: async (): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>(
      '/hr/attendance/clock-in'
    );
    return res.data;
  },

  clockOut: async (): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>(
      '/hr/attendance/clock-out'
    );
    return res.data;
  },

  getMyAttendance: async (): Promise<AttendanceLogDto[]> => {
    const res =
      await apiClient.get<ApiResponse<AttendanceLogDto[]>>('/hr/attendance/my');
    return res.data?.data ?? [];
  },

  getAttendance: async (
    search?: string,
    startDate?: string,
    endDate?: string
  ): Promise<AdminAttendanceLogDto[]> => {
    const res = await apiClient.get<ApiResponse<AdminAttendanceLogDto[]>>(
      '/admin/hr/attendance',
      {
        params: {
          ...(search ? { search } : {}),
          ...(startDate ? { startDate } : {}),
          ...(endDate ? { endDate } : {}),
        },
      }
    );
    return res.data?.data ?? [];
  },

  // US3: Vacation Request & Approvals
  submitVacation: async (payload: {
    startDate: string;
    endDate: string;
    reason: string;
  }): Promise<ApiResponse<string>> => {
    const res = await apiClient.post<ApiResponse<string>>(
      '/hr/vacations',
      payload
    );
    return res.data;
  },

  getMyVacations: async (): Promise<VacationDto[]> => {
    const res =
      await apiClient.get<ApiResponse<VacationDto[]>>('/hr/vacations/my');
    return res.data?.data ?? [];
  },

  getVacations: async (
    search?: string,
    status?: string
  ): Promise<AdminVacationDto[]> => {
    const res = await apiClient.get<ApiResponse<AdminVacationDto[]>>(
      '/admin/hr/vacations',
      {
        params: {
          ...(search ? { search } : {}),
          ...(status ? { status } : {}),
        },
      }
    );
    return res.data?.data ?? [];
  },

  approveVacation: async (id: string): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(
      `/admin/hr/vacations/${id}/approve`
    );
    return res.data;
  },

  rejectVacation: async (id: string): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(
      `/admin/hr/vacations/${id}/reject`
    );
    return res.data;
  },
};

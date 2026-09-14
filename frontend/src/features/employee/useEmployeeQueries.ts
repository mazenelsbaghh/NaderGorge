'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { invalidateMany, registerCacheStore } from '@/lib/cache-invalidation';
import { queryKeys } from '@/lib/query-keys';
import {
  adminService,
  AdminCreateUserPayload,
  AdminCreateUserResult,
} from '@/services/admin-service';
import {
  EmployeeDto,
  hrService,
  SaveEmployeeProfilePayload,
  EmployeeProfileMutationResult,
  ProvisionEmployeePayload,
  ProvisionEmployeeResult,
} from '@/services/hr-service';

const cacheKey = (key: readonly unknown[]) => key.map(String).join(':');
const employeesKey = cacheKey(queryKeys.employees.all);

function employeeListKey(search?: string) {
  return cacheKey(queryKeys.employees.list(search?.trim() || 'all'));
}

function employeeDetailKey(userId: string) {
  return cacheKey(queryKeys.employees.detail(userId));
}

function employeeInvalidationKeys(userId?: string) {
  return [
    employeesKey,
    cacheKey(queryKeys.hr.all),
    cacheKey(queryKeys.session),
    ...(userId ? [employeeDetailKey(userId)] : []),
  ];
}

export function useEmployees(search?: string) {
  const normalizedSearch = search?.trim() || undefined;
  const key = employeeListKey(normalizedSearch);
  const [data, setData] = useState<EmployeeDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

  const refetch = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      setData(await hrService.listEmployees(normalizedSearch));
    } catch (cause) {
      setError(cause);
      throw cause;
    } finally {
      setIsLoading(false);
    }
  }, [normalizedSearch]);

  useEffect(() => {
    let active = true;
    const load = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await hrService.listEmployees(normalizedSearch);
        if (active) setData(result);
      } catch (cause) {
        if (active) setError(cause);
      } finally {
        if (active) setIsLoading(false);
      }
    };

    void load();
    const cleanupCacheStore = registerCacheStore(key, () => setData([]), () => void refetch());
    return () => {
      active = false;
      cleanupCacheStore();
    };
  }, [key, normalizedSearch, refetch]);

  return { data, isLoading, error, refetch };
}

export function useEmployee(userId: string | undefined) {
  const employees = useEmployees();
  return useMemo(() => ({
    data: userId ? employees.data.find((employee) => employee.userId === userId || employee.id === userId) ?? null : null,
    isLoading: employees.isLoading,
    error: employees.error,
    refetch: employees.refetch,
  }), [employees.data, employees.error, employees.isLoading, employees.refetch, userId]);
}

export function useCreateEmployee() {
  const [isPending, setIsPending] = useState(false);

  const mutateAsync = useCallback(async (payload: AdminCreateUserPayload): Promise<AdminCreateUserResult> => {
    setIsPending(true);
    try {
      const response = await adminService.createUser(payload);
      if (!response.success || !response.data) throw new Error(response.message || 'تعذر إنشاء المستخدم');
      invalidateMany(employeeInvalidationKeys(response.data.id));
      return response.data;
    } finally {
      setIsPending(false);
    }
  }, []);

  return { mutateAsync, isPending };
}

export function useProvisionEmployee() {
  const [isPending, setIsPending] = useState(false);

  const mutateAsync = useCallback(async (payload: ProvisionEmployeePayload): Promise<ProvisionEmployeeResult> => {
    setIsPending(true);
    try {
      const response = await hrService.provisionEmployee(payload);
      if (!response.success || !response.data) throw new Error(response.message || 'تعذر إنشاء الموظف');
      invalidateMany(employeeInvalidationKeys(response.data.userId));
      return response.data;
    } finally {
      setIsPending(false);
    }
  }, []);

  return { mutateAsync, isPending };
}

export function useUpdateEmployeeProfile() {
  const [isPending, setIsPending] = useState(false);

  const mutateAsync = useCallback(async (payload: SaveEmployeeProfilePayload): Promise<EmployeeProfileMutationResult> => {
    setIsPending(true);
    try {
      const response = await hrService.saveEmployeeProfile(payload);
      if (!response.success || !response.data) throw new Error(response.message || 'تعذر حفظ ملف الموظف');
      invalidateMany(employeeInvalidationKeys(payload.userId));
      return response.data;
    } finally {
      setIsPending(false);
    }
  }, []);

  return { mutateAsync, isPending };
}

export function useDisableEmployee() {
  const [isPending, setIsPending] = useState(false);

  const mutateAsync = useCallback(async ({ userId, status }: { userId: string; status: string }) => {
    setIsPending(true);
    try {
      const response = await adminService.updateUserStatus(userId, status);
      invalidateMany(employeeInvalidationKeys(userId));
      return response;
    } finally {
      setIsPending(false);
    }
  }, []);

  return { mutateAsync, isPending };
}

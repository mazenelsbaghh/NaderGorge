import { useAuthStore } from "@/stores/auth-store";

type PermissionUser = ReturnType<typeof useAuthStore.getState>['user'];

export function evaluatePermission(user: PermissionUser, permission?: string): boolean {
  if (!permission) return true;
  if (!user) return false;
  const roles = user.roles || [];
  if (roles.some((role) => ['admin', 'superadmin'].includes(role.toLowerCase()))) return true;
  return (user.permissions || []).includes(permission);
}

export function evaluateStaffAccess(user: PermissionUser): boolean {
  const hasStaffRole = user?.roles?.some((role) => {
    const normalized = role.toLowerCase();
    return normalized.includes('staff') || normalized.includes('assistant') ||
      normalized.includes('admin') || normalized.includes('supervisor') ||
      normalized === 'employee';
  });

  return !!hasStaffRole || !!user?.permissions?.some((permission) =>
    permission.startsWith('hr.self.') || permission === 'hr.attendance.self');
}

export function useHasPermission() {
  const { user } = useAuthStore();

  const hasPermission = (permission: string | undefined): boolean => {
    return evaluatePermission(user, permission);
  };

  return { hasPermission };
}

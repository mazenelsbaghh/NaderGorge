import { useAuthStore } from "@/stores/auth-store";

export function useHasPermission() {
  const { user } = useAuthStore();

  const hasPermission = (permission: string | undefined): boolean => {
    if (!permission) return true;
    if (!user) return false;

    // Admin role bypasses all permission restrictions
    const roles = user.roles || [];
    if (roles.includes("Admin")) {
      return true;
    }

    // Check if the user has the permission claim
    const permissions = user.permissions || [];
    return permissions.includes(permission);
  };

  return { hasPermission };
}

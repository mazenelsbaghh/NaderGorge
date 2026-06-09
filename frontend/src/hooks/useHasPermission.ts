import { useAuthStore } from "@/stores/auth-store";

export function useHasPermission() {
  const { user } = useAuthStore();

  const hasPermission = (permission: string | undefined): boolean => {
    if (!permission) return true;
    if (!user) return false;

    // Admin and Teacher roles bypass all permission restrictions
    const roles = user.roles || [];
    if (roles.includes("Admin") || roles.includes("Teacher")) {
      return true;
    }

    // Check if the user has the permission claim
    const permissions = user.permissions || [];
    return permissions.includes(permission);
  };

  return { hasPermission };
}
